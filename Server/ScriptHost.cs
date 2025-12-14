using Shared;
using Core;
using System;
using System.IO;
using System.Threading;
using Core.VM;
using Core.VM.Runtime;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Linq;

namespace Server
{
    public class ScriptHost : IHostedService, IDisposable, IScriptHost
    {
        private readonly IProject _project;
        private FileSystemWatcher? _watcher;
        private Timer? _debounceTimer;
        private readonly object _scriptLock = new object();
        private readonly ServerSettings _settings;
        private readonly System.Collections.Concurrent.ConcurrentQueue<(string, Action<string>)> _commandQueue = new();
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ScriptHost> _logger;
        private ScriptingEnvironment? _currentEnvironment;
        private CancellationTokenSource? _cancellationTokenSource;

        public ScriptHost(IProject project, ServerSettings settings, IServiceProvider serviceProvider, ILogger<ScriptHost> logger)
        {
            _project = project;
            _settings = settings;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting script host...");
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _watcher = new FileSystemWatcher(_project.GetFullPath(Constants.ScriptsRoot))
            {
                Filter = "*.*",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };
            _debounceTimer = new Timer(ReloadScripts, null, Timeout.Infinite, Timeout.Infinite);

            ReloadScripts(null); // Initial script load
            _watcher.Changed += OnScriptChanged;
            _watcher.Created += OnScriptChanged;
            _watcher.Deleted += OnScriptChanged;
            _watcher.Renamed += OnScriptChanged;
            _logger.LogInformation($"Watching for changes in '{_project.GetFullPath(Constants.ScriptsRoot)}' directory.");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource?.Cancel();
            _watcher?.Dispose();
            _debounceTimer?.Dispose();
            return Task.CompletedTask;
        }


        public void Tick()
        {
            Tick(_currentEnvironment?.ScriptManager.GetAllGameObjects() ?? Enumerable.Empty<IGameObject>(), processGlobals: true);
        }

        public void Tick(IEnumerable<IGameObject> objectsToTick, bool processGlobals = false)
        {
            if (processGlobals)
                ProcessCommandQueue();

            var threads = GetThreads();
            var remainingThreads = ExecuteThreads(threads, objectsToTick, processGlobals);
            UpdateThreads(remainingThreads);
        }

        public List<IScriptThread> GetThreads()
        {
            lock (_scriptLock)
            {
                return _currentEnvironment?.Threads.ToList() ?? new List<IScriptThread>();
            }
        }

        public void UpdateThreads(IEnumerable<IScriptThread> threads)
        {
            lock (_scriptLock)
            {
                if (_currentEnvironment != null)
                {
                    _currentEnvironment.Threads.Clear();
                    _currentEnvironment.Threads.AddRange(threads);
                }
            }
        }

        public IEnumerable<IScriptThread> ExecuteThreads(IEnumerable<IScriptThread> threads, IEnumerable<IGameObject> objectsToTick, bool processGlobals = false)
        {
            var objectIds = new HashSet<int>(objectsToTick.Select(o => o.Id));
            var dreamThreads = threads.OfType<DreamThread>().ToList();
            var nextThreads = new System.Collections.Concurrent.ConcurrentBag<IScriptThread>();
            var budgetMs = 1000.0 / _settings.Performance.TickRate * _settings.Performance.TimeBudgeting.ScriptHost.BudgetPercent;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            foreach (var thread in dreamThreads)
            {
                if (stopwatch.Elapsed.TotalMilliseconds >= budgetMs && _settings.Performance.TimeBudgeting.ScriptHost.Enabled)
                {
                    nextThreads.Add(thread); // Add unprocessed threads back to the list
                    continue;
                }

                bool shouldProcess = (processGlobals && thread.AssociatedObject == null) || (thread.AssociatedObject != null && objectIds.Contains(thread.AssociatedObject.Id));

                if (shouldProcess)
                {
                    var state = thread.Run(_settings.Performance.VmInstructionSlice);
                    if (state == DreamThreadState.Running)
                    {
                        nextThreads.Add(thread);
                    }
                    else
                    {
                        _logger.LogDebug($"Thread for proc '{thread.CurrentProc.Name}' finished with state: {state}");
                    }
                }
                else
                {
                    nextThreads.Add(thread);
                }
            }

            foreach (var thread in threads.Where(t => t is not DreamThread))
            {
                nextThreads.Add(thread);
            }

            return nextThreads;
        }

        public void EnqueueCommand(string command, Action<string> onResult)
        {
            _commandQueue.Enqueue((command, onResult));
        }

        public void AddThread(IScriptThread thread)
        {
            lock (_scriptLock)
            {
                _currentEnvironment?.Threads.Add(thread);
            }
        }

        private void ProcessCommandQueue()
        {
            while (_commandQueue.TryDequeue(out var commandInfo))
            {
                var (command, onResult) = commandInfo;
                string? result;
                lock (_scriptLock)
                {
                    result = _currentEnvironment?.ScriptManager.ExecuteCommand(command);
                }
                onResult(result ?? "Command executed with no result.");
            }
        }

        private void OnScriptChanged(object source, FileSystemEventArgs e)
        {
            var ext = Path.GetExtension(e.FullPath).ToLower();
            if (ext == ".lua" || ext == ".dm" || ext == ".cs")
            {
                _logger.LogInformation($"File {e.FullPath} has been changed. Debouncing reload...");
                _debounceTimer?.Change(_settings.Development.ScriptReloadDebounceMs, Timeout.Infinite);
            }
        }

        private void ReloadScripts(object? state)
        {
            _logger.LogInformation("Starting background script reload...");
            Task.Run(async () =>
            {
                if (_cancellationTokenSource?.IsCancellationRequested ?? true)
                    return;
                try
                {
                    var newEnvironment = new ScriptingEnvironment(_serviceProvider);
                    await newEnvironment.Initialize();

                    lock (_scriptLock)
                    {
                        _currentEnvironment?.Dispose();
                        _currentEnvironment = newEnvironment;
                    }
                    _logger.LogInformation("Script reload complete and activated.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during background script reload.");
                }
            });
        }


        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
