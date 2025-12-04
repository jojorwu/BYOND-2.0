using Core;
using System;
using System.IO;
using System.Threading;
using Core.VM;
using Core.VM.Runtime;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Server
{
    public class ScriptHost : IDisposable, IScriptHost
    {
        private readonly Project _project;
        private readonly FileSystemWatcher _watcher;
        private readonly Timer _debounceTimer;
        private readonly object _scriptLock = new object();
        private readonly ServerSettings _settings;
        private readonly ILogger<ScriptHost> _logger;
        private readonly ILogger<ScriptingEnvironment> _scriptingEnvironmentLogger;
        private readonly System.Collections.Concurrent.ConcurrentQueue<(string, Action<string>)> _commandQueue = new();
        private readonly IServiceProvider _serviceProvider;
        private ScriptingEnvironment? _currentEnvironment;

        public ScriptHost(Project project, IOptions<ServerSettings> settings, IServiceProvider serviceProvider, ILogger<ScriptHost> logger, ILogger<ScriptingEnvironment> scriptingEnvironmentLogger)
        {
            _project = project;
            _settings = settings.Value;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _scriptingEnvironmentLogger = scriptingEnvironmentLogger;

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
            _logger.LogInformation("Starting script host...");
            ReloadScripts(null); // Initial script load
            _watcher.Changed += OnScriptChanged;
            _watcher.Created += OnScriptChanged;
            _watcher.Deleted += OnScriptChanged;
            _watcher.Renamed += OnScriptChanged;
            _logger.LogInformation("Watching for changes in '{ScriptsRoot}' directory.", _project.GetFullPath(Constants.ScriptsRoot));
        }

        public void Tick()
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
                    _logger.LogInformation("Thread for proc '{ProcName}' finished with state: {State}", thread.CurrentProc.Name, state);
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
                _logger.LogInformation("File {FullPath} has been changed. Debouncing reload...", e.FullPath);
                _debounceTimer.Change(_settings.Development.ScriptReloadDebounceMs, Timeout.Infinite);
            }
        }

        private void ReloadScripts(object? state)
        {
            _logger.LogInformation("Starting background script reload...");
            Task.Run(async () =>
            {
                try
                {
                    var newEnvironment = new ScriptingEnvironment(_serviceProvider, _scriptingEnvironmentLogger);
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
                    _logger.LogError(ex, "Error during background script reload");
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
