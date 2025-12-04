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
        private readonly ILogger<ScriptingEnvironment> _logger;
        public IGameApi GameApi { get; }
        public ObjectTypeManager ObjectTypeManager { get; }
        public DreamVM DreamVM { get; }
        public ScriptManager ScriptManager { get; }
        public List<DreamThread> Threads { get; } = new();

        private readonly IServiceScope _scope;

        public ScriptingEnvironment(IServiceProvider serviceProvider, ILogger<ScriptingEnvironment> logger)
        {
            _scope = serviceProvider.CreateScope();
            var scopeProvider = _scope.ServiceProvider;
            _logger = logger;

            GameApi = scopeProvider.GetRequiredService<IGameApi>();
            ObjectTypeManager = scopeProvider.GetRequiredService<ObjectTypeManager>();
            DreamVM = scopeProvider.GetRequiredService<DreamVM>();
            ScriptManager = scopeProvider.GetRequiredService<ScriptManager>();
        }

        public async Task Initialize()
        {
            await ScriptManager.ReloadAll();
            ScriptManager.InvokeGlobalEvent("OnStart");

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
