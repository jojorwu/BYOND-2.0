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
            _watcher?.Dispose();
            _debounceTimer?.Dispose();
            return Task.CompletedTask;
        }


        public virtual void Tick()
        {
            ProcessCommandQueue();

            ScriptingEnvironment? environment;
            lock (_scriptLock)
            {
                environment = _currentEnvironment;
            }
            if (environment == null) return;

            var threadsToRun = new Queue<DreamThread>(environment.Threads);
            var finishedThreads = new HashSet<DreamThread>();
            var budgetMs = 1000.0 / _settings.Performance.TickRate * _settings.Performance.TimeBudgeting.ScriptHost.BudgetPercent;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            while (threadsToRun.Count > 0)
            {
                if (stopwatch.Elapsed.TotalMilliseconds >= budgetMs && _settings.Performance.TimeBudgeting.ScriptHost.Enabled)
                    break;

                var thread = threadsToRun.Dequeue();
                var state = thread.Run(_settings.Performance.VmInstructionSlice);

                if (state != DreamThreadState.Running)
                {
                    _logger.LogDebug($"Thread for proc '{thread.CurrentProc.Name}' finished with state: {state}");
                    finishedThreads.Add(thread);
                }
                else
                {
                    threadsToRun.Enqueue(thread);
                }
            }

            if (finishedThreads.Count > 0)
            {
                lock (_scriptLock)
                {
                    _currentEnvironment?.Threads.RemoveAll(t => finishedThreads.Contains(t));
                }
            }
        }

        void IScriptHost.EnqueueCommand(string command, Action<string> onResult)
        {
            _commandQueue.Enqueue((command, onResult));
        }

        public void AddThread(DreamThread thread)
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
                lock (_scriptLock)
                {
                    _currentEnvironment?.ScriptManager.ExecuteCommand(command);
                }
                onResult("Command executed."); // Simplified result
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
            GC.SuppressFinalize(this);
        }
    }
}
