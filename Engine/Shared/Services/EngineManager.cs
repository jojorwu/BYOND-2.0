using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace Shared.Services;
    public class EngineManager : EngineService, IEngineManager
    {
        private string _basePath;
        private const string SettingsFileName = "launcher_settings.json";
        private readonly ILogger<EngineManager>? _logger;
        private readonly IEnumerable<IAsyncInitializable> _initializableServices;

        public EngineManager(IEnumerable<IAsyncInitializable> initializableServices, ILogger<EngineManager>? logger = null, string? basePath = null)
        {
            _initializableServices = initializableServices;
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

        private static readonly Dictionary<EngineComponent, string> ComponentNames = new()
        {
            { EngineComponent.Client, "Client" },
            { EngineComponent.Server, "Server" },
            { EngineComponent.Compiler, "Compiler" },
            { EngineComponent.Editor, "Editor" }
        };

        public string GetExecutablePath(EngineComponent component)
        {
            if (!ComponentNames.TryGetValue(component, out var baseName))
                throw new ArgumentOutOfRangeException(nameof(component));

            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var exeName = isWindows ? $"{baseName}.exe" : baseName;
            var dllName = $"{baseName}.dll";

            var searchPaths = new[]
            {
                Path.Combine(_basePath, exeName),
                Path.Combine(_basePath, dllName),
                Path.Combine(_basePath, "bin", exeName),
                Path.Combine(_basePath, "bin", dllName)
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path)) return path;
            }

            return exeName;
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

        protected override Task OnInitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task ShutdownAsync()
        {
            return Task.CompletedTask;
        }
    }
