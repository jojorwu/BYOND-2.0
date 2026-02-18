using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shared;
using Server.Events;
using Shared.Messaging;

namespace Server
{
    public class ScriptEnvironmentManager : IScriptEnvironmentManager
    {
        private readonly IScriptWatcher _scriptWatcher;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ScriptEnvironmentManager> _logger;
        private readonly IEventBus _eventBus;
        private readonly object _scriptLock = new();
        private ScriptingEnvironment? _currentEnvironment;
        private int _isReloading = 0;
        private CancellationTokenSource? _cancellationTokenSource;

        public event Action? OnEnvironmentReloaded;

        public ScriptEnvironmentManager(IScriptWatcher scriptWatcher, IServiceProvider serviceProvider, ILogger<ScriptEnvironmentManager> logger, IEventBus eventBus)
        {
            _scriptWatcher = scriptWatcher;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _eventBus = eventBus;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _eventBus.Subscribe<ReloadScriptsEvent>(OnReloadEvent);
            _scriptWatcher.Start();

            ReloadScripts(); // Initial load
            return Task.CompletedTask;
        }

        private void OnReloadEvent(ReloadScriptsEvent e) => ReloadScripts();

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
            _scriptWatcher.Stop();
            _eventBus.Unsubscribe<ReloadScriptsEvent>(OnReloadEvent);
        }

        public IScriptManager? GetCurrentScriptManager()
        {
            lock (_scriptLock)
            {
                return _currentEnvironment?.ScriptManager;
            }
        }

        public IScriptThread[] GetActiveThreads()
        {
            var env = _currentEnvironment;
            if (env == null) return Array.Empty<IScriptThread>();

            lock (_scriptLock)
            {
                return env.Threads.ToArray();
            }
        }

        public void UpdateActiveThreads(IEnumerable<IScriptThread> threads)
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

        public void AddThread(IScriptThread thread)
        {
            lock (_scriptLock)
            {
                _currentEnvironment?.Threads.Add(thread);
            }
        }

        private void ReloadScripts()
        {
            if (Interlocked.CompareExchange(ref _isReloading, 1, 0) != 0)
            {
                _logger.LogDebug("Script reload already in progress. Skipping.");
                return;
            }

            _logger.LogInformation("Starting background script reload...");
            Task.Run(async () =>
            {
                try
                {
                    if (_cancellationTokenSource?.IsCancellationRequested ?? true)
                        return;

                    var newEnvironment = new ScriptingEnvironment(_serviceProvider);
                    await newEnvironment.Initialize();

                    lock (_scriptLock)
                    {
                        _currentEnvironment?.Dispose();
                        _currentEnvironment = newEnvironment;
                    }
                    _logger.LogInformation("Script reload complete and activated.");
                    OnEnvironmentReloaded?.Invoke();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during background script reload.");
                }
                finally
                {
                    Interlocked.Exchange(ref _isReloading, 0);
                }
            });
        }
    }
}
