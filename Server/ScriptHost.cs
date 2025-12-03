using Core;
using System;
using System.IO;
using System.Threading;
using Core.VM;
using Core.VM.Runtime;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace Server
{
    public class ScriptHost : IDisposable, IScriptHost
    {
        private readonly Project _project;
        private readonly FileSystemWatcher _watcher;
        private readonly Timer _debounceTimer;
        private readonly object _scriptLock = new object();
        private readonly GameState _gameState;
        private readonly ServerSettings _settings;
        private ScriptEnvironment _activeEnvironment;
        private readonly System.Collections.Concurrent.ConcurrentQueue<(string, Action<string>)> _commandQueue = new();
        private readonly IServiceProvider _serviceProvider;

        public ScriptHost(Project project, GameState gameState, ServerSettings settings, IServiceProvider serviceProvider)
        {
            _project = project;
            _gameState = gameState;
            _settings = settings;
            _serviceProvider = serviceProvider;
            _activeEnvironment = new ScriptEnvironment(serviceProvider);

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

            var budgetMs = 1000.0 / _settings.Performance.TickRate * _settings.Performance.TimeBudgeting.ScriptHost.BudgetPercent;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            ScriptEnvironment currentEnv;
            lock(_scriptLock)
                currentEnv = _activeEnvironment;

            // Create a copy of the queue to iterate over, so we can modify the original
            var threadsToRun = new Queue<DreamThread>(currentEnv.Threads);
            currentEnv.Threads.Clear();

            while (threadsToRun.Count > 0)
            {
                if (stopwatch.Elapsed.TotalMilliseconds >= budgetMs && _settings.Performance.TimeBudgeting.ScriptHost.Enabled)
                {
                    // If budget is exceeded, re-queue the remaining threads
                    while(threadsToRun.Count > 0)
                        currentEnv.Threads.Enqueue(threadsToRun.Dequeue());
                    break;
                }

                var thread = threadsToRun.Dequeue();
                var state = thread.Run(_settings.Performance.VmInstructionSlice);

                if (state == DreamThreadState.Running)
                {
                    currentEnv.Threads.Enqueue(thread); // Re-queue the thread if it's still running
                }
                else
                {
                    Console.WriteLine($"Thread for proc '{thread.CurrentProc.Name}' finished with state: {state}");
                }
            }
        }

        public void EnqueueCommand(string command, Action<string> onResult)
        {
            _commandQueue.Enqueue((command, onResult));
        }

        public void AddThread(DreamThread thread)
        {
            lock (_scriptLock)
            {
                _activeEnvironment.Threads.Enqueue(thread);
            }
        }

        private void ProcessCommandQueue()
        {
            while (_commandQueue.TryDequeue(out var commandInfo))
            {
                var (command, onResult) = commandInfo;
                lock(_scriptLock)
                    _activeEnvironment.ScriptManager.ExecuteCommand(command);

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
                var newEnvironment = new ScriptEnvironment(_serviceProvider);
                try
                {
                    await newEnvironment.ScriptManager.ReloadAll();
                    Console.WriteLine("Invoking OnStart event in new environment...");
                    newEnvironment.ScriptManager.InvokeGlobalEvent("OnStart");

                    var mainThread = newEnvironment.ScriptManager.CreateThread("world.New");
                    if (mainThread != null)
                    {
                        newEnvironment.Threads.Enqueue(mainThread);
                        Console.WriteLine("Successfully created 'world.New' thread in new environment.");
                    }
                    else
                    {
                        Console.WriteLine("Warning: Could not create 'world.New' thread in new environment.");
                    }

                    // Hot-swap the old environment with the new one
                    ScriptEnvironment oldEnvironment;
                    lock (_scriptLock)
                    {
                        oldEnvironment = _activeEnvironment;
                        _activeEnvironment = newEnvironment;
                    }

                    // Dispose of the old environment
                    oldEnvironment.Dispose();

                    Console.WriteLine("Script reload complete and activated.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during background script reload: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                    // If reload fails, dispose the new environment as it's not being used
                    newEnvironment.Dispose();
                }
            });
        }


        public void Dispose()
        {
            _watcher.Changed -= OnScriptChanged;
            _watcher.Dispose();
            _debounceTimer.Dispose();
            _activeEnvironment.Dispose();
        }
    }
}
