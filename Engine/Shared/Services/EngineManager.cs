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
    public class EngineManager : IEngineManager
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

        public async Task InitializeAsync()
        {
            _logger?.LogInformation("Initializing engine services...");
            foreach (var service in _initializableServices)
            {
                _logger?.LogDebug("Initializing service: {ServiceType}", service.GetType().Name);
                await service.InitializeAsync();
            }
            _logger?.LogInformation("Engine initialization complete.");
        }

        public async Task ShutdownAsync()
        {
            _logger?.LogInformation("Shutting down engine services...");
            // We might want to shutdown in reverse order, but IAsyncInitializable doesn't have ShutdownAsync yet.
            // If we add IAsyncShutdownable, we can use it here.
            await Task.CompletedTask;
            _logger?.LogInformation("Engine shutdown complete.");
        }
    }
