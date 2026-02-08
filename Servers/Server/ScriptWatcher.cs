using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared;
using Core;

namespace Server
{
    public class ScriptWatcher : IScriptWatcher
    {
        private readonly IProject _project;
        private readonly ServerSettings _settings;
        private readonly ILogger<ScriptWatcher> _logger;
        private FileSystemWatcher? _watcher;
        private Timer? _debounceTimer;

        public event Action? OnReloadRequested;

        public ScriptWatcher(IProject project, IOptions<ServerSettings> settings, ILogger<ScriptWatcher> logger)
        {
            _project = project;
            _settings = settings.Value;
            _logger = logger;
        }

        public void Start()
        {
            var scriptsPath = _project.GetFullPath(Constants.ScriptsRoot);
            if (!Directory.Exists(scriptsPath))
            {
                _logger.LogWarning($"Scripts directory not found at '{scriptsPath}'. Script watching disabled.");
                return;
            }

            _watcher = new FileSystemWatcher(scriptsPath)
            {
                Filter = "*.*",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            _debounceTimer = new Timer(HandleReload, null, Timeout.Infinite, Timeout.Infinite);

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
            _watcher.Renamed += OnFileChanged;

            _logger.LogInformation($"Watching for changes in '{scriptsPath}' directory.");
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            var ext = Path.GetExtension(e.FullPath).ToLower();
            if (ext == ".lua" || ext == ".dm" || ext == ".cs")
            {
                _logger.LogInformation($"File {e.FullPath} has been changed. Debouncing reload...");
                _debounceTimer?.Change(_settings.Development.ScriptReloadDebounceMs, Timeout.Infinite);
            }
        }

        private void HandleReload(object? state)
        {
            OnReloadRequested?.Invoke();
        }

        public void Stop()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnFileChanged;
                _watcher.Created -= OnFileChanged;
                _watcher.Deleted -= OnFileChanged;
                _watcher.Renamed -= OnFileChanged;
                _watcher.Dispose();
                _watcher = null;
            }

            if (_debounceTimer != null)
            {
                _debounceTimer.Dispose();
                _debounceTimer = null;
            }
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
