using Shared;
using Core;
using Core.VM;
using Core.VM.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Server
{
    public class ScriptingEnvironment : IDisposable
    {
        public IGameApi GameApi { get; }
        public IObjectTypeManager ObjectTypeManager { get; }
        public DreamVM DreamVM { get; }
        public IScriptManager ScriptManager { get; }
        public List<IScriptThread> Threads { get; } = new();

        private readonly IServiceScope _scope;
        private readonly ILogger<ScriptingEnvironment> _logger;

        public ScriptingEnvironment(IServiceProvider serviceProvider)
        {
            _scope = serviceProvider.CreateScope();
            var scopeProvider = _scope.ServiceProvider;

            GameApi = scopeProvider.GetRequiredService<IGameApi>();
            ObjectTypeManager = scopeProvider.GetRequiredService<IObjectTypeManager>();
            DreamVM = scopeProvider.GetRequiredService<DreamVM>();
            ScriptManager = scopeProvider.GetRequiredService<IScriptManager>();
            _logger = scopeProvider.GetRequiredService<ILogger<ScriptingEnvironment>>();
        }

        public async Task Initialize()
        {
            await ScriptManager.ReloadAll();
            var onStartThreads = ScriptManager.InvokeGlobalEvent("OnStart");
            Threads.AddRange(onStartThreads);

            var mainThread = ScriptManager.CreateThread("world.New");
            if (mainThread != null)
            {
                Threads.Add(mainThread);
                _logger.LogInformation("Successfully created 'world.New' thread.");
            }
            else
            {
                _logger.LogWarning("Could not create 'world.New' thread.");
            }
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }
}
