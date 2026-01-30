using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace Shared.Services
{
    public class EngineManager : IEngineManager
    {
        private string _basePath;
        private const string SettingsFileName = "launcher_settings.json";
        private readonly ILogger<EngineManager>? _logger;

        public EngineManager(ILogger<EngineManager>? logger = null, string? basePath = null)
        {
            _logger = logger;
            _basePath = basePath ?? AppDomain.CurrentDomain.BaseDirectory;
            LoadSettings();
        }

        public string GetBaseEnginePath() => _basePath;

        public void SetBaseEnginePath(string path)
        {
            _basePath = path;
            _logger?.LogInformation("Engine base path set to: {Path}", path);
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
                _logger?.LogError(e, "Error loading launcher settings");
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
                _logger?.LogError(e, "Error saving launcher settings");
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

            string path = Path.Combine(_basePath, name);
            if (File.Exists(path)) return path;

            path = Path.Combine(_basePath, "bin", name);
            if (File.Exists(path)) return path;

            return name;
        }

        public bool IsComponentInstalled(EngineComponent component)
        {
            var path = GetExecutablePath(component);
            return File.Exists(path) || !path.Contains(Path.DirectorySeparatorChar);
        }

        public void InstallComponent(EngineComponent component)
        {
            _logger?.LogInformation("Requesting installation of component: {Component}", component);
        }
    }
}
