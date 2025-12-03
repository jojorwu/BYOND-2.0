using Core;
using Core.VM.Runtime;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace Server
{
    public class ScriptEnvironment : IDisposable
    {
        public readonly IServiceScope Scope;
        public readonly ObjectTypeManager ObjectTypeManager;
        public readonly DreamVM DreamVM;
        public readonly ScriptManager ScriptManager;
        public readonly IGameApi GameApi;
        public readonly Queue<DreamThread> Threads = new();

        public ScriptEnvironment(IServiceProvider serviceProvider)
        {
            Scope = serviceProvider.CreateScope();
            var scopeProvider = Scope.ServiceProvider;
            ObjectTypeManager = scopeProvider.GetRequiredService<ObjectTypeManager>();
            DreamVM = scopeProvider.GetRequiredService<DreamVM>();
            GameApi = scopeProvider.GetRequiredService<IGameApi>();
            ScriptManager = scopeProvider.GetRequiredService<ScriptManager>();
        }

        public void Dispose()
        {
            Scope.Dispose();
        }
    }
}
