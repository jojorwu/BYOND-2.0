using Shared;
using Core;
using System;
using System.Threading;
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
        private readonly IScriptWatcher _scriptWatcher;
        private readonly object _scriptLock = new object();
        private readonly ServerSettings _settings;
        private readonly System.Collections.Concurrent.ConcurrentQueue<(string, Action<string>)> _commandQueue = new();
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ScriptHost> _logger;
        private readonly IGameState _gameState;
        private ScriptingEnvironment? _currentEnvironment;
        private CancellationTokenSource? _cancellationTokenSource;

        public ScriptHost(IScriptWatcher scriptWatcher, ServerSettings settings, IServiceProvider serviceProvider, ILogger<ScriptHost> logger, IGameState gameState)
        {
            _scriptWatcher = scriptWatcher;
            _settings = settings;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _gameState = gameState;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting script host...");
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _scriptWatcher.OnReloadRequested += OnReloadRequested;
            _scriptWatcher.Start();

            ReloadScripts(); // Initial script load
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource?.Cancel();
            _scriptWatcher.Stop();
            _scriptWatcher.OnReloadRequested -= OnReloadRequested;
            return Task.CompletedTask;
        }

        private void OnReloadRequested()
        {
            ReloadScripts();
        }

        public void Tick()
        {
            Tick(_gameState.GetAllGameObjects(), processGlobals: true);
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
                    nextThreads.Add(thread);
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

        private void ReloadScripts()
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
