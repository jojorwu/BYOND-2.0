using Core.VM.Runtime;

using System;
using Core.VM.Runtime;

namespace Core
{
    public interface IScriptHost
    {
        void AddThread(DreamThread thread);
        void EnqueueCommand(string command, Action<string> onResult);
    }
}
