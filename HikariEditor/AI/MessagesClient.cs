using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Core;
using Anthropic.Models.Messages;

namespace HikariEditor;

// 公式 Anthropic .NET SDK の Messages API で tool_use を実行しながら最終回答まで
// ループするエージェントエンジン。会話履歴を内部に保持する。
//
// Anthropic SDK の C# 向けストリーミングは tool_use ブロックの逐次組み立てが
// 公開ヘルパーで提供されていないため、ここでは非ストリーミングの Create を用いる。
// テキストは 1 ターン分まとめて OnText へ渡す。
internal class MessagesClient : IChatEngine
{
    const int MaxTurns = 25;
    const int MaxTokens = 8192;

    readonly string _workingDir;
    readonly AnthropicClient _client;
    readonly string _model;
    readonly string _system;
    readonly List<ToolUnion> _tools = [];
    readonly List<MessageParam> _messages = [];

    public Action<string>? OnText { get; set; }
    public Action<string>? OnTool { get; set; }

    public MessagesClient(ModelConfig config, string workingDir)
    {
        _workingDir = workingDir;
        _model = config.Model;
        _system = ChatEngine.SystemPrompt(workingDir);
        // 環境変数 ANTHROPIC_AUTH_TOKEN（Claude Code などが設定する OAuth トークン）があると、
        // SDK はそれを Authorization: Bearer として送り、x-api-key と二重認証になって API に
        // 拒否される（x-api-key header is required）。ApiKey のみ明示し AuthToken を null へ
        // 固定することで、設定した API キーだけを x-api-key として送る。
        // キー未入力でも x-api-key を欠かさないよう、空ならダミーを送る（キー不要な互換サーバー向け）。
        string apiKey = string.IsNullOrEmpty(config.ApiKey) ? "dummy" : config.ApiKey;
        ClientOptions options = new() { ApiKey = apiKey, AuthToken = null };

        // Azure OpenAI 互換など api-key ヘッダーで認証する Anthropic 互換サーバー向け。
        // ExtraHeaders は x-api-key を上書きしないが、別名の api-key は追加されるため共存できる。
        if (config.UseApiKeyHeader)
            options.ExtraHeaders = new Dictionary<string, string> { ["api-key"] = config.ApiKey };

        // Endpoint を指定すれば Anthropic 互換サーバーへ向けられる（未指定なら本家）。
        // SDK は BaseUrl へ /v1/messages を付与するため、OpenAI 流儀で末尾に /v1 を含む
        // ベース URL でも /v1/v1/messages と二重にならないよう取り除く。
        if (!string.IsNullOrEmpty(config.Endpoint))
        {
            string baseUrl = config.Endpoint.TrimEnd('/');
            if (baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                baseUrl = baseUrl[..^3];
            options.BaseUrl = baseUrl;
        }

        _client = new AnthropicClient(options);

        foreach (AITools.ToolSpec s in AITools.Specs)
            _tools.Add(new Tool
            {
                Name = s.Name,
                Description = s.Description,
                InputSchema = BuildSchema(s.Schema),
            });
    }

    public async Task SendAsync(string userText, CancellationToken ct)
    {
        _messages.Add(new MessageParam { Role = Role.User, Content = userText });

        for (int turn = 0; turn < MaxTurns; turn++)
        {
            MessageCreateParams parameters = new()
            {
                Model = _model,
                MaxTokens = MaxTokens,
                System = _system,
                Tools = _tools,
                Messages = _messages,
            };

            Message response = await _client.Messages.Create(parameters, cancellationToken: ct);

            // 応答ブロックをアシスタント発言として再構築しつつ、tool_use を実行する。
            List<ContentBlockParam> assistantContent = [];
            List<ContentBlockParam> toolResults = [];
            foreach (ContentBlock block in response.Content)
            {
                if (block.TryPickText(out TextBlock? text))
                {
                    assistantContent.Add(new TextBlockParam { Text = text.Text });
                    if (text.Text.Length > 0)
                        OnText?.Invoke(text.Text);
                }
                else if (block.TryPickToolUse(out ToolUseBlock? toolUse))
                {
                    assistantContent.Add(new ToolUseBlockParam
                    {
                        ID = toolUse.ID,
                        Name = toolUse.Name,
                        Input = toolUse.Input,
                    });
                    string args = JsonSerializer.Serialize(toolUse.Input);
                    OnTool?.Invoke(AITools.Describe(toolUse.Name, args));
                    string result = AITools.Execute(toolUse.Name, args, _workingDir);
                    toolResults.Add(new ToolResultBlockParam
                    {
                        ToolUseID = toolUse.ID,
                        Content = result,
                    });
                }
            }

            _messages.Add(new MessageParam { Role = Role.Assistant, Content = assistantContent });

            // tool_use が無ければこのリクエストは完了
            if (toolResults.Count == 0)
                return;

            _messages.Add(new MessageParam { Role = Role.User, Content = toolResults });
        }
    }

    // ツールの JSON スキーマ文字列から Anthropic の InputSchema（properties / required）を作る。
    static InputSchema BuildSchema(string schemaJson)
    {
        using JsonDocument doc = JsonDocument.Parse(schemaJson);
        JsonElement root = doc.RootElement;

        Dictionary<string, JsonElement> properties = [];
        if (root.TryGetProperty("properties", out JsonElement props))
            foreach (JsonProperty p in props.EnumerateObject())
                properties[p.Name] = p.Value.Clone();

        List<string> required = [];
        if (root.TryGetProperty("required", out JsonElement req))
            foreach (JsonElement e in req.EnumerateArray())
                required.Add(e.GetString() ?? string.Empty);

        return new InputSchema { Properties = properties, Required = required };
    }
}
