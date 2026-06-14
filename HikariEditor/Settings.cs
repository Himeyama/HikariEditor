using System.IO;
using System.Text.Json;

namespace HikariEditor;

internal class Settings
{
    public string ExplorerDir { get; set; } = string.Empty;
    public bool AutoSave { get; set; } = false;
    public string OpenDirPath { get; set; } = string.Empty;
    public bool LogOpen { get; set; } = false;
    public bool AiPanelOpen { get; set; } = false;
    public double TerminalHeight { get; set; } = 300;
    public double ExplorerWidth { get; set; } = 360;
    public double AiPanelWidth { get; set; } = 400;

    static string SettingPath => Path.Combine(Path.GetTempPath(), "HikariEditor-settings.json");

    public void SaveSetting()
    {
        string jsonString = JsonSerializer.Serialize(this);
        FileItem fileItem = new(SettingPath);
        fileItem.Save(jsonString, "LF");
    }

    public void LoadSetting()
    {
        if (!File.Exists(SettingPath))
        {
            SaveSetting();
            return;
        }
        string jsonString = File.ReadAllText(SettingPath);
        Settings? settings = JsonSerializer.Deserialize<Settings>(jsonString);
        if (settings is null) return;
        ExplorerDir = settings.ExplorerDir;
        AutoSave = settings.AutoSave;
        OpenDirPath = settings.OpenDirPath;
        LogOpen = settings.LogOpen;
        AiPanelOpen = settings.AiPanelOpen;
        TerminalHeight = settings.TerminalHeight;
        ExplorerWidth = settings.ExplorerWidth;
        AiPanelWidth = settings.AiPanelWidth;
    }
}
