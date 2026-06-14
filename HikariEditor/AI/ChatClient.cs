using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Chat;
using OpenAIChatClient = OpenAI.Chat.ChatClient;
using OpenAIChatMessage = OpenAI.Chat.ChatMessage;   // 本プロジェクトの ChatMessage と区別する

namespace HikariEditor;

// 公式 OpenAI .NET SDK で Chat Completions をストリーミングし、tool_calls を実行しながら
// 最終回答までループするエージェントエンジン。会話履歴を内部に保持する。
internal class ChatCompletionsClient : IChatEngine
{
    // ツールが無限に呼ばれ続けるのを防ぐための 1 リクエスト当たりの上限。
    const int MaxTurns = 25;

    readonly string _workingDir;
    readonly OpenAIChatClient _chat;
    readonly List<OpenAIChatMessage> _messages = [];
    readonly ChatCompletionOptions _options = new();

    public Action<string>? OnText { get; set; }
    public Action<string>? OnTool { get; set; }

    public ChatCompletionsClient(ModelConfig config, string workingDir, bool rewriteMaxTokens = false)
    {
        _workingDir = workingDir;

        // 公式 SDK は本家 OpenAI 以外（ローカルサーバー等の OpenAI 互換 API）でも
        // Endpoint を差し替えれば使える。Endpoint は /chat/completions を除いたベース URL。
        // キー無しのローカルサーバー向けに、空なら認証情報にプレースホルダを渡す。
        ApiKeyCredential credential = new(string.IsNullOrEmpty(config.ApiKey) ? "no-key" : config.ApiKey);
        OpenAIClientOptions clientOptions = new();
        if (!string.IsNullOrEmpty(config.Endpoint))
            clientOptions.Endpoint = new Uri(config.Endpoint);
        // Azure OpenAI 互換 API は Authorization ではなく api-key ヘッダーで認証する。
        // 認証ヘッダー付与後（PerCall）に api-key を載せる。
        // Azure OpenAI は api-key が既定なので、トグルに関わらず常に api-key ヘッダーで送る。
        if (config.UseApiKeyHeader || config.Api == ApiKind.AzureOpenAI)
            clientOptions.AddPolicy(new ApiKeyHeaderPolicy(config.ApiKey), PipelinePosition.PerCall);
        // Azure OpenAI は max_tokens を受け付けず max_completion_tokens を要求するため、
        // リクエストボディに max_tokens があれば送信直前に置き換える。
        if (rewriteMaxTokens)
            clientOptions.AddPolicy(new MaxTokensRewritePolicy(), PipelinePosition.PerCall);
        _chat = new OpenAIChatClient(config.Model, credential, clientOptions);

        foreach (AITools.ToolSpec s in AITools.Specs)
            _options.Tools.Add(ChatTool.CreateFunctionTool(s.Name, s.Description, BinaryData.FromString(s.Schema)));

        _messages.Add(new SystemChatMessage(ChatEngine.SystemPrompt(workingDir)));
    }

    public async Task SendAsync(string userText, CancellationToken ct)
    {
        _messages.Add(new UserChatMessage(userText));

        for (int turn = 0; turn < MaxTurns; turn++)
        {
            (string text, List<StreamedToolCall> toolCalls) = await StreamTurnAsync(ct);

            // アシスタントの発言を履歴へ追加（テキストとツール呼び出しの両方を保持）
            AssistantChatMessage assistant = new(text ?? string.Empty);
            foreach (StreamedToolCall c in toolCalls)
                assistant.ToolCalls.Add(
                    ChatToolCall.CreateFunctionToolCall(c.Id, c.Name, BinaryData.FromString(c.Arguments)));
            _messages.Add(assistant);

            // ツール呼び出しが無ければこのリクエストは完了
            if (toolCalls.Count == 0)
                return;

            // 各ツールを実行し、結果を tool ロールで履歴へ返す
            foreach (StreamedToolCall c in toolCalls)
            {
                OnTool?.Invoke(AITools.Describe(c.Name, c.Arguments));
                string result = AITools.Execute(c.Name, c.Arguments, _workingDir);
                _messages.Add(new ToolChatMessage(c.Id, result));
            }
        }
    }

    // 1 ターン分のストリームを読み、テキストと（あれば）ツール呼び出しを返す。
    // ツール呼び出しは index ごとに id・名前・引数の断片が分割して届くため貯め合わせる。
    async Task<(string text, List<StreamedToolCall> toolCalls)> StreamTurnAsync(CancellationToken ct)
    {
        StringBuilder text = new();
        Dictionary<int, string> ids = [];
        Dictionary<int, string> names = [];
        Dictionary<int, StringBuilder> arguments = [];

        AsyncCollectionResult<StreamingChatCompletionUpdate> updates =
            _chat.CompleteChatStreamingAsync(_messages, _options, ct);

        await foreach (StreamingChatCompletionUpdate update in updates)
        {
            foreach (ChatMessageContentPart part in update.ContentUpdate)
            {
                if (part.Text is { Length: > 0 } chunk)
                {
                    text.Append(chunk);
                    OnText?.Invoke(chunk);
                }
            }

            foreach (StreamingChatToolCallUpdate tc in update.ToolCallUpdates)
            {
                if (tc.ToolCallId is { Length: > 0 } id)
                    ids[tc.Index] = id;
                if (tc.FunctionName is { Length: > 0 } name)
                    names[tc.Index] = name;
                if (tc.FunctionArgumentsUpdate is { } argsChunk)
                {
                    if (!arguments.TryGetValue(tc.Index, out StringBuilder? sb))
                        arguments[tc.Index] = sb = new StringBuilder();
                    sb.Append(argsChunk.ToString());
                }
            }
        }

        List<StreamedToolCall> calls = [];
        foreach (int index in ids.Keys)
            calls.Add(new StreamedToolCall(
                ids[index],
                names.GetValueOrDefault(index, string.Empty),
                arguments.TryGetValue(index, out StringBuilder? sb) ? sb.ToString() : string.Empty));
        return (text.ToString(), calls);
    }

    readonly record struct StreamedToolCall(string Id, string Name, string Arguments);
}

// API キーを `api-key` ヘッダーへ載せて送るパイプラインポリシー。
// OpenAI SDK の既定では Authorization: Bearer で認証するため、Azure OpenAI など
// api-key ヘッダーを要求するエンドポイント向けにヘッダーを上書きする。
internal sealed class ApiKeyHeaderPolicy(string apiKey) : PipelinePolicy
{
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        message.Request.Headers.Set("api-key", apiKey);
        ProcessNext(message, pipeline, currentIndex);
    }

    public override ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        message.Request.Headers.Set("api-key", apiKey);
        return ProcessNextAsync(message, pipeline, currentIndex);
    }
}

// リクエストボディ（JSON）に max_tokens があれば max_completion_tokens へ置き換える
// パイプラインポリシー。Azure OpenAI は max_tokens を受け付けず max_completion_tokens を
// 要求するため、OpenAI SDK が出力する max_tokens を送信直前に書き換える。
internal sealed class MaxTokensRewritePolicy : PipelinePolicy
{
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        Rewrite(message);
        ProcessNext(message, pipeline, currentIndex);
    }

    public override ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        Rewrite(message);
        return ProcessNextAsync(message, pipeline, currentIndex);
    }

    static void Rewrite(PipelineMessage message)
    {
        BinaryContent? content = message.Request.Content;
        if (content is null)
            return;

        using MemoryStream buffer = new();
        content.WriteTo(buffer, default);

        // JSON 以外（マルチパート等）はそのまま通す。パースに失敗しても送信は妨げない。
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(buffer.ToArray());
        }
        catch (JsonException)
        {
            return;
        }

        if (root is not JsonObject obj || obj["max_tokens"] is not JsonNode value)
            return;

        // JsonNode は単一の親しか持てないため、付け替え前に切り離す。
        obj.Remove("max_tokens");
        obj["max_completion_tokens"] = value.DeepClone();

        message.Request.Content = BinaryContent.Create(BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes(obj)));
    }
}
