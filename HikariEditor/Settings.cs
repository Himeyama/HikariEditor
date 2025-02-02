using System.IO;
using System.Text.Json;

namespace HikariEditor
{
    internal class Settings
    {
        public string ExplorerDir { get; set; } = string.Empty;
        public bool AutoSave { get; set; } = false;
        public string OpenDirPath { get; set; } = string.Empty;

        public Settings()
        {
            /* 再帰となるような関数禁止 */
        }

        public void SaveSetting()
        {
            string SettingPath = $"{Path.GetTempPath()}\\HikariEditor-settings.json";
            string jsonString = JsonSerializer.Serialize(this);
            FileItem fileItem = new(SettingPath);
            fileItem.Save(jsonString, "LF");
        }

        public void LoadSetting()
        {
            string SettingPath = $"{Path.GetTempPath()}\\HikariEditor-settings.json";
            if (!File.Exists(SettingPath))
            {
                SaveSetting();
                return;
            }
            string jsonString = File.ReadAllText(SettingPath);
            Settings? settings = JsonSerializer.Deserialize<Settings>(jsonString);
            ExplorerDir = settings!.ExplorerDir;
            AutoSave = settings.AutoSave;
            OpenDirPath = settings.OpenDirPath;
        }
    }
}
