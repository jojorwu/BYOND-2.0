using System;
using System.Collections.Generic;

namespace Shared
{
    public interface IScriptHost
    {
        [Obsolete("Use Tick(IEnumerable<IGameObject>, bool) instead.")]
        void Tick();
        void Tick(IEnumerable<IGameObject> objectsToTick, bool processGlobals = false);
        void EnqueueCommand(string command, Action<string> onResult);
        void AddThread(IScriptThread thread);
    }
}
