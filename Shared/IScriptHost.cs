using System;
using System.Collections.Generic;

namespace Shared
{
    public interface IScriptHost
    {
        [Obsolete("Use Tick(IEnumerable<IGameObject>) instead.")]
        void Tick();
        void Tick(IEnumerable<IGameObject> objectsToTick);
        void EnqueueCommand(string command, Action<string> onResult);
        void AddThread(IScriptThread thread);
    }
}
