using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Shared
{
    public class EngineManager : IEngineManager
    {
        private string _basePath;
        private const string SettingsFileName = "launcher_settings.json";

        public EngineManager(string? basePath = null)
        {
            _basePath = basePath ?? AppDomain.CurrentDomain.BaseDirectory;
            LoadSettings();
        }

        public string GetBaseEnginePath() => _basePath;

        public void SetBaseEnginePath(string path)
        {
            _basePath = path;
            SaveSettings();
        }

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFileName))
                {
                    var json = File.ReadAllText(SettingsFileName);
                    var settings = JsonSerializer.Deserialize<LauncherSettings>(json);
                    if (settings?.EnginePath != null)
                    {
                        _basePath = settings.EnginePath;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error loading settings: {e.Message}");
            }
        }

        public void SaveSettings()
        {
            try
            {
                var settings = new LauncherSettings { EnginePath = _basePath };
                var json = JsonSerializer.Serialize(settings);
                File.WriteAllText(SettingsFileName, json);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error saving settings: {e.Message}");
            }
        }

        private class LauncherSettings
        {
            public string? EnginePath { get; set; }
        }

        public string GetExecutablePath(EngineComponent component)
        {
            string name = component switch
            {
                EngineComponent.Client => "Client",
                EngineComponent.Server => "Server",
                EngineComponent.Compiler => "Compiler",
                EngineComponent.Editor => "Editor",
                _ => throw new ArgumentOutOfRangeException(nameof(component))
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                name += ".exe";
            }

            // Check current directory first
            string path = Path.Combine(_basePath, name);
            if (File.Exists(path)) return path;

            // Check bin folder (common in dev environments)
            path = Path.Combine(_basePath, "bin", name);
            if (File.Exists(path)) return path;

            return name; // Fallback to just the name, maybe it's in PATH
        }

        public bool IsComponentInstalled(EngineComponent component)
        {
            var path = GetExecutablePath(component);
            return File.Exists(path) || !path.Contains(Path.DirectorySeparatorChar);
        }

        public void InstallComponent(EngineComponent component)
        {
            // Placeholder for actual installation logic (e.g., downloading from a server)
            Console.WriteLine($"Installing {component}...");
        }
    }
}
