using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
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
