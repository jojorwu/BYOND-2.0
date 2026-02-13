using Shared;
using System;
using System.Threading;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Linq;
using Shared.Services;

namespace Server
{
    public class ScriptHost : EngineService, IHostedService, IDisposable, IScriptHost
    {
        public override int Priority => 50;

        private readonly IScriptEnvironmentManager _envManager;
        private readonly IScriptScheduler _scheduler;
        private readonly IScriptCommandProcessor _commandProcessor;
        private readonly ILogger<ScriptHost> _logger;
        private readonly IGameState _gameState;

        public ScriptHost(
            IScriptEnvironmentManager envManager,
            IScriptScheduler scheduler,
            IScriptCommandProcessor commandProcessor,
            ILogger<ScriptHost> logger,
            IGameState gameState)
        {
            _envManager = envManager;
            _scheduler = scheduler;
            _commandProcessor = commandProcessor;
            _logger = logger;
            _gameState = gameState;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting script host coordinator...");
            await _envManager.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _envManager.Stop();
            return Task.CompletedTask;
        }

        public async Task TickAsync()
        {
            ProcessCommandQueue();

            var objectIds = new HashSet<int>();
            _gameState.ForEachGameObject(o => objectIds.Add(o.Id));

            var threads = _envManager.GetActiveThreads();
            var remainingThreads = await _scheduler.ExecuteThreadsAsync(threads, Array.Empty<IGameObject>(), true, objectIds);
            _envManager.UpdateActiveThreads(remainingThreads);
        }

        public async Task TickAsync(IEnumerable<IGameObject> objectsToTick, bool processGlobals = false)
        {
            if (processGlobals)
                ProcessCommandQueue();

            var threads = _envManager.GetActiveThreads();
            var remainingThreads = await _scheduler.ExecuteThreadsAsync(threads, objectsToTick, processGlobals);
            _envManager.UpdateActiveThreads(remainingThreads);
        }

        public List<IScriptThread> GetThreads()
        {
            return _envManager.GetActiveThreads().ToList();
        }

        public void UpdateThreads(IEnumerable<IScriptThread> threads)
        {
            _envManager.UpdateActiveThreads(threads);
        }

        public Task<IEnumerable<IScriptThread>> ExecuteThreadsAsync(IEnumerable<IScriptThread> threads, IEnumerable<IGameObject> objectsToTick, bool processGlobals = false, HashSet<int>? objectIds = null)
        {
            return _scheduler.ExecuteThreadsAsync(threads, objectsToTick, processGlobals, objectIds);
        }

        public void EnqueueCommand(string command, Action<string> onResult)
        {
            _commandProcessor.EnqueueCommand(command, onResult);
        }

        public void AddThread(IScriptThread thread)
        {
            _envManager.AddThread(thread);
        }

        private void ProcessCommandQueue()
        {
            var scriptManager = _envManager.GetCurrentScriptManager();
            if (scriptManager != null)
            {
                _commandProcessor.ProcessCommands(scriptManager);
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
