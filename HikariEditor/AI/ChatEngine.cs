using System;
using System.Threading;
using System.Threading.Tasks;

namespace HikariEditor;

// チャットエンジンの共通インターフェース。AIPanel はこの抽象に対して送信し、
// API 種別ごとの実装（Chat Completions / Responses / Messages）を差し替える。
internal interface IChatEngine
{
    // UI スレッドへのマーシャリングは呼び出し側（AIPanel）が担う。
    Action<string>? OnText { get; set; }   // アシスタントのテキスト断片
    Action<string>? OnTool { get; set; }   // ツール実行（表示用ラベル）

    Task SendAsync(string userText, CancellationToken ct);
}

internal static class ChatEngine
{
    // アクティブモデルの API 種別に応じたエンジンを生成する。
    public static IChatEngine Create(ModelConfig config, string workingDir) => config.Api switch
    {
        ApiKind.Responses => new ChatCompletionsClient(config, workingDir),
        // Azure OpenAI は OpenAI ライブラリの Chat Completions をそのまま使う（URL を柔軟に
        // 差し替えられるため）。ただしリクエストボディの max_tokens は受け付けないため、
        // max_completion_tokens へ書き換える。
        ApiKind.AzureOpenAI => new ChatCompletionsClient(config, workingDir, rewriteMaxTokens: true),
        ApiKind.Messages => new MessagesClient(config, workingDir),
        _ => new ChatCompletionsClient(config, workingDir),
    };

    // 全エンジン共通のシステムプロンプト。
    public static string SystemPrompt(string workingDir) =>
        "あなたはコードエディタに組み込まれた AI コーディングアシスタントです。" +
        $"作業ディレクトリは {workingDir} です。" +
        "ファイルの読み書きには Read / Write / Edit ツールを使ってください。" +
        "パスは作業ディレクトリからの相対パスで指定できます。";
}
