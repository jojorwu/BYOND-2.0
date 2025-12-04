using Core;
using Core.VM;
using Core.VM.Runtime;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Server
{
    public class ScriptingEnvironment : IDisposable
    {
        public IGameApi GameApi { get; }
        public ObjectTypeManager ObjectTypeManager { get; }
        public DreamVM DreamVM { get; }
        public ScriptManager ScriptManager { get; }
        public List<DreamThread> Threads { get; } = new();

        private readonly IServiceScope _scope;

        public ScriptingEnvironment(IServiceProvider serviceProvider)
        {
            _scope = serviceProvider.CreateScope();
            var scopeProvider = _scope.ServiceProvider;

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
                Console.WriteLine("Successfully created 'world.New' thread.");
            }
            else
            {
                Console.WriteLine("Warning: Could not create 'world.New' thread.");
            }
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }
}
