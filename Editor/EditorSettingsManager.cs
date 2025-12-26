using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Editor
{
    public class EditorSettingsManager : IEditorSettingsManager
    {
        private const string SettingsFileName = "editor_settings.json";
        private static readonly string SettingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BYOND2.0", SettingsFileName);

        public EditorSettings Settings { get; private set; }

        public EditorSettingsManager()
        {
            Load();
        }

        private void Load()
        {
            if (File.Exists(SettingsFilePath))
            {
                try
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    Settings = JsonSerializer.Deserialize<EditorSettings>(json) ?? new EditorSettings();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to load editor settings: {e.Message}");
                    Settings = new EditorSettings();
                }
            }
            else
            {
                Settings = new EditorSettings();
            }
        }

        public void Save()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(Settings, options);
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath));
            File.WriteAllText(SettingsFilePath, json);
        }
    }
}
