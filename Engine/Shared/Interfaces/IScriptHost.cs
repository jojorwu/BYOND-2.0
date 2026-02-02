using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;
using System;

namespace Shared.Interfaces
{
    public interface IScriptHost
    {
        void Tick();
        void Tick(IEnumerable<IGameObject> objectsToTick, bool processGlobals = false);
        void EnqueueCommand(string command, Action<string> onResult);
        void AddThread(IScriptThread thread);
        List<IScriptThread> GetThreads();
        void UpdateThreads(IEnumerable<IScriptThread> threads);
        IEnumerable<IScriptThread> ExecuteThreads(IEnumerable<IScriptThread> threads, IEnumerable<IGameObject> objectsToTick, bool processGlobals = false, HashSet<int>? objectIds = null);
    }
}
