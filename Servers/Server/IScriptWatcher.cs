using System;

namespace Server
{
    public interface IScriptWatcher : IDisposable
    {
        void Start();
        void Stop();
        event Action OnReloadRequested;
    }
}
