using Core;
using System;
using System.IO;
using System.Threading;

namespace Server
{
    public class ScriptHost : IDisposable
    {
        private readonly Scripting _scripting;
        private readonly FileSystemWatcher _watcher;
        private readonly Timer _debounceTimer;
        private readonly object _scriptLock = new object();
        private readonly GameState _gameState;
        private readonly GameApi _gameApi;
        private readonly Project _project;
        private readonly ObjectTypeManager _objectTypeManager;
        private readonly MapLoader _mapLoader;

        public ScriptHost(Project project)
        {
            _project = project;
            _gameState = new GameState();
            _objectTypeManager = new ObjectTypeManager(_project);
            _objectTypeManager.LoadTypes();
            _mapLoader = new MapLoader(_objectTypeManager, _project);
            _gameApi = new GameApi(_gameState, _objectTypeManager, _mapLoader, _project);
            _scripting = new Scripting(_gameApi);
            _watcher = new FileSystemWatcher(_project.GetFullPath("scripts"))
            {
                Filter = "*.lua",
                NotifyFilter = NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            _debounceTimer = new Timer(ReloadScripts, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start()
        {
            Console.WriteLine("Starting script host...");
            ReloadScripts(null); // Initial script load
            _watcher.Changed += OnScriptChanged;
            Console.WriteLine($"Watching for changes in '{_project.GetFullPath("scripts")}' directory.");
        }

        private void OnScriptChanged(object source, FileSystemEventArgs e)
        {
            Console.WriteLine($"File {e.FullPath} has been changed. Debouncing reload...");
            _debounceTimer.Change(200, Timeout.Infinite);
        }

        private void ReloadScripts(object? state)
        {
            lock (_scriptLock)
            {
                try
                {
                    Console.WriteLine("Reloading scripts...");
                    _scripting.Reload();
                    _scripting.ExecuteFile(_project.GetFullPath(Path.Combine("scripts", "main.lua")));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error executing script: {ex.Message}");
                }
            }
        }

        public string ExecuteCommand(string command)
        {
            lock (_scriptLock)
            {
                try
                {
                    return _scripting.ExecuteCommand(command);
                }
                catch (Exception ex)
                {
                    return $"Error: {ex.Message}";
                }
            }
        }

        public void Dispose()
        {
            _watcher.Dispose();
            _debounceTimer.Dispose();
            _scripting.Dispose();
        }
    }
}
