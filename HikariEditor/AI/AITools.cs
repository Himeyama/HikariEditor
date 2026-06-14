using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OpenAI.Chat;

namespace HikariEditor;

// エージェントが使えるビルトインツール（Read / Write / Edit）。
// 作業ディレクトリ（エクスプローラーで開いているフォルダ）を基準に相対パスを解決する。
internal static class AITools
{
    // 公式 OpenAI SDK の ChatTool 定義。ChatClient がリクエストオプションに載せる。
    // パラメータの JSON スキーマは BinaryData として渡す。
    public static IReadOnlyList<ChatTool> Definitions() =>
    [
        ChatTool.CreateFunctionTool(
            "Read",
            "作業ディレクトリ内のファイルを読み取り、内容を返す。",
            BinaryData.FromString("""
            {
              "type": "object",
              "properties": {
                "path": { "type": "string", "description": "読み取るファイルのパス（作業ディレクトリからの相対パスまたは絶対パス）" }
              },
              "required": ["path"]
            }
            """)),
        ChatTool.CreateFunctionTool(
            "Write",
            "ファイルを新規作成または上書きする。親ディレクトリが無ければ作成する。",
            BinaryData.FromString("""
            {
              "type": "object",
              "properties": {
                "path": { "type": "string", "description": "書き込むファイルのパス" },
                "content": { "type": "string", "description": "ファイルに書き込む全内容" }
              },
              "required": ["path", "content"]
            }
            """)),
        ChatTool.CreateFunctionTool(
            "Edit",
            "ファイル内の old_string を new_string に置換する。old_string はファイル内で一意である必要がある。",
            BinaryData.FromString("""
            {
              "type": "object",
              "properties": {
                "path": { "type": "string", "description": "編集するファイルのパス" },
                "old_string": { "type": "string", "description": "置換対象の文字列（一意であること）" },
                "new_string": { "type": "string", "description": "置換後の文字列" }
              },
              "required": ["path", "old_string", "new_string"]
            }
            """))
    ];

    // ツール 1 件を実行し、LLM へ返すテキスト結果を得る。
    // 失敗（ファイル I/O・不正な引数）はそのままエラーメッセージとしてモデルに戻し、
    // モデルが回復行動を取れるようにする。
    public static string Execute(string name, string argumentsJson, string workingDir)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            JsonElement args = doc.RootElement;
            return name switch
            {
                "Read" => Read(Resolve(args, "path", workingDir)),
                "Write" => Write(Resolve(args, "path", workingDir), GetString(args, "content")),
                "Edit" => Edit(Resolve(args, "path", workingDir), GetString(args, "old_string"), GetString(args, "new_string")),
                _ => $"エラー: 未知のツール '{name}'"
            };
        }
        catch (Exception ex)
        {
            return $"エラー: {ex.Message}";
        }
    }

    // 表示用のラベル（会話の Tool 行に出す）。
    public static string Describe(string name, string argumentsJson)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            string? path = doc.RootElement.TryGetProperty("path", out JsonElement p) ? p.GetString() : null;
            return path is null ? name : $"{name}: {path}";
        }
        catch
        {
            return name;
        }
    }

    static string Read(string path)
    {
        if (!File.Exists(path))
            return $"エラー: ファイルが存在しません: {path}";
        return File.ReadAllText(path);
    }

    static string Write(string path, string content)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, content);
        return $"書き込み成功: {path}";
    }

    static string Edit(string path, string oldString, string newString)
    {
        if (!File.Exists(path))
            return $"エラー: ファイルが存在しません: {path}";
        string text = File.ReadAllText(path);
        int first = text.IndexOf(oldString, StringComparison.Ordinal);
        if (first < 0)
            return "エラー: old_string がファイル内に見つかりません。";
        if (oldString.Length > 0 && text.IndexOf(oldString, first + oldString.Length, StringComparison.Ordinal) >= 0)
            return "エラー: old_string が複数箇所に一致します。一意になるよう前後を含めてください。";
        File.WriteAllText(path, text.Remove(first, oldString.Length).Insert(first, newString));
        return $"編集成功: {path}";
    }

    // 相対パスは作業ディレクトリ基準、絶対パスはそのまま使う。
    static string Resolve(JsonElement args, string key, string workingDir)
    {
        string path = GetString(args, key);
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException($"引数 '{key}' が指定されていません。");
        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(workingDir, path));
    }

    static string GetString(JsonElement args, string key) =>
        args.TryGetProperty(key, out JsonElement el) && el.ValueKind == JsonValueKind.String
            ? el.GetString() ?? string.Empty
            : string.Empty;
}
