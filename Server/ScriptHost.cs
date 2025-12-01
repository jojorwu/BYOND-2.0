using Core;
using System;
using System.IO;
using System.Threading;
using Core.VM.Runtime;
using System.Collections.Generic;

namespace Server
{
    public class ScriptHost : IDisposable
    {
        private readonly Project _project;
        private readonly FileSystemWatcher _watcher;
        private readonly Timer _debounceTimer;
        private readonly object _scriptLock = new object();
        private readonly GameState _gameState;
        private GameApi _gameApi;
        private ObjectTypeManager _objectTypeManager;
        private DreamVM _dreamVM;
        private readonly ServerSettings _settings;
        private readonly List<DreamThread> _threads = new();
        private ScriptManager _scriptManager;
        private readonly System.Collections.Concurrent.ConcurrentQueue<(string, Action<string>)> _commandQueue = new();

        public ScriptHost(Project project, GameState gameState, ServerSettings settings)
        {
            _project = project;
            _gameState = gameState;
            _settings = settings;
            _objectTypeManager = new ObjectTypeManager();
            _dreamVM = new DreamVM(settings);

            var mapLoader = new MapLoader(_objectTypeManager);
            _gameApi = new GameApi(_project, _gameState, _objectTypeManager, mapLoader);
            _scriptManager = new ScriptManager(_gameApi, _objectTypeManager, _project, _dreamVM);

            _watcher = new FileSystemWatcher(_project.GetFullPath(Constants.ScriptsRoot))
            {
                Filter = "*.*",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };
            _debounceTimer = new Timer(ReloadScripts, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start()
        {
            Console.WriteLine("Starting script host...");
            ReloadScripts(null); // Initial script load
            _watcher.Changed += OnScriptChanged;
            _watcher.Created += OnScriptChanged;
            _watcher.Deleted += OnScriptChanged;
            _watcher.Renamed += OnScriptChanged;
            Console.WriteLine($"Watching for changes in '{_project.GetFullPath(Constants.ScriptsRoot)}' directory.");
        }

        public void Tick()
        {
            ProcessCommandQueue();

            lock (_scriptLock)
            {
                // Simple round-robin scheduler for VM threads
                for (int i = _threads.Count - 1; i >= 0; i--)
                {
                    var thread = _threads[i];
                    var state = thread.Run(100); // 100 instructions per tick budget

                    if (state != DreamThreadState.Running)
                    {
                        Console.WriteLine($"Thread for proc '{thread.CurrentProc.Name}' finished with state: {state}");
                        _threads.RemoveAt(i);
                    }
                }
            }
        }

        public void EnqueueCommand(string command, Action<string> onResult)
        {
            _commandQueue.Enqueue((command, onResult));
        }

        private void ProcessCommandQueue()
        {
            while (_commandQueue.TryDequeue(out var commandInfo))
            {
                var (command, onResult) = commandInfo;
                _scriptManager.ExecuteCommand(command);
                onResult("Command executed."); // Simplified result
            }
        }

        private void OnScriptChanged(object source, FileSystemEventArgs e)
        {
            var ext = Path.GetExtension(e.FullPath).ToLower();
            if (ext == ".lua" || ext == ".dm" || ext == ".cs")
            {
                Console.WriteLine($"File {e.FullPath} has been changed. Debouncing reload...");
                _debounceTimer.Change(_settings.Development.ScriptReloadDebounceMs, Timeout.Infinite);
            }
        }

        private void ReloadScripts(object? state)
        {
            Console.WriteLine("Starting background script reload...");
            Task.Run(async () =>
            {
                try
                {
                    // 1. Create a new script environment in the background
                    var newObjectTypeManager = new ObjectTypeManager();
                    var newDreamVM = new DreamVM(_settings);
                    var newMapLoader = new MapLoader(newObjectTypeManager);
                    var newGameApi = new GameApi(_project, _gameState, newObjectTypeManager, newMapLoader);
                    var newScriptManager = new ScriptManager(newGameApi, newObjectTypeManager, _project, newDreamVM);

                    await newScriptManager.ReloadAll();
                    Console.WriteLine("Invoking OnStart event in new environment...");
                    newScriptManager.InvokeGlobalEvent("OnStart");

                    var newThreads = new List<DreamThread>();
                    var mainThread = newScriptManager.CreateThread("world.New");
                    if (mainThread != null)
                    {
                        newThreads.Add(mainThread);
                        Console.WriteLine("Successfully created 'world.New' thread in new environment.");
                    }
                    else
                    {
                        Console.WriteLine("Warning: Could not create 'world.New' thread in new environment.");
                    }

                    // 2. Hot-swap the old environment with the new one inside a lock
                    lock (_scriptLock)
                    {
                        _objectTypeManager = newObjectTypeManager;
                        _dreamVM = newDreamVM;
                        _gameApi = newGameApi;
                        _scriptManager = newScriptManager;
                        _threads.Clear();
                        _threads.AddRange(newThreads);
                    }
                    Console.WriteLine("Script reload complete and activated.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during background script reload: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                }
            });
        }

        public void Dispose()
        {
            _watcher.Changed -= OnScriptChanged;
            _watcher.Dispose();
            _debounceTimer.Dispose();
        }
    }
}
