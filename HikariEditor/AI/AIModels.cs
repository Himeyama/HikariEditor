using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HikariEditor;

// LLM の API 種別。OpenAI 互換の Chat Completions / Responses と Anthropic の Messages。
// 種別が増えても保存済み JSON を壊さないよう文字列で永続化する。
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ApiKind
{
    ChatCompletions,
    Responses,
    Messages
}

// 登録された 1 つの LLM 設定。
// Endpoint は /chat/completions を除いたベース（例: http://localhost/v1）を保持する。
// 設定一覧（ListView）で設定名をライブ更新するため Name のみ変更通知する。
public class ModelConfig : System.ComponentModel.INotifyPropertyChanged
{
    // 設定一覧での識別子。再起動をまたいでアクティブモデルを指し直すために使う。
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    string _name = string.Empty;
    public string Name                                       // 設定名（表示用）
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Name)));
        }
    }
    public ApiKind Api { get; set; } = ApiKind.ChatCompletions;
    public string Endpoint { get; set; } = string.Empty;    // 例: http://localhost/v1
    public string Model { get; set; } = string.Empty;       // 例: gpt-4o-mini
    public string ApiKey { get; set; } = string.Empty;

    // 末尾のスラッシュ有無に関わらず /chat/completions を 1 度だけ付与する。
    [JsonIgnore]
    public string ChatCompletionsUrl => $"{Endpoint.TrimEnd('/')}/chat/completions";

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

// 登録モデル群とアクティブモデルの選択状態。
// API キーを含むため %TEMP% の Settings.cs とは分け、%LOCALAPPDATA% に保存する。
internal class AIConfig
{
    public List<ModelConfig> Models { get; set; } = [];
    public string ActiveModelId { get; set; } = string.Empty;

    static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HikariEditor");

    static string ConfigPath => Path.Combine(ConfigDir, "ai-models.json");

    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [JsonIgnore]
    public ModelConfig? ActiveModel =>
        Models.FirstOrDefault(m => m.Id == ActiveModelId) ?? Models.FirstOrDefault();

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public static AIConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new AIConfig();
        try
        {
            return JsonSerializer.Deserialize<AIConfig>(File.ReadAllText(ConfigPath)) ?? new AIConfig();
        }
        catch
        {
            // 壊れた設定ファイルは空設定として扱い、起動を妨げない
            return new AIConfig();
        }
    }
}
