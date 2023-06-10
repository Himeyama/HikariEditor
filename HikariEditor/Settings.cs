using System.IO;
using System.Text.Json;

namespace HikariEditor
{
    internal class Settings
    {
        string settingPath = string.Empty;
        public string explorerDir { get; set; } = string.Empty;
        public bool autoSave { get; set; } = false;
        public string openDirPath { get; set; } = string.Empty;

        public Settings()
        {
            settingPath = $"{Path.GetTempPath()}\\HikariEditor-settings.json";
            /* 再帰となるような関数禁止 */
        }

        public void SaveSetting()
        {
            string jsonString = JsonSerializer.Serialize(this);
            FileItem fileItem = new(settingPath);
            fileItem.Save(jsonString, "LF");
        }

        public void LoadSetting()
        {
            if (!File.Exists(settingPath))
            {
                SaveSetting();
                return;
            }
            string jsonString = File.ReadAllText(settingPath);
            Settings settings = JsonSerializer.Deserialize<Settings>(jsonString);
            explorerDir = settings.explorerDir;
            autoSave = settings.autoSave;
            openDirPath = settings.openDirPath;
        }
    }
}
