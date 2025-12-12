using System;
using System.Collections.Generic;

namespace Shared
{
    public interface IGameState : IDisposable
    {
        SpatialGrid SpatialGrid { get; }
        IMap? Map { get; set; }
        Dictionary<int, GameObject> GameObjects { get; }
        IDisposable ReadLock();
        IDisposable WriteLock();
        string GetSnapshot();
        string GetDeltaSnapshot();
        void Reset();
    }
}
