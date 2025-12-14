using System;
using System.Collections.Generic;

using System.Collections.Generic;

namespace Shared
{
    public interface IGameState : IDisposable
    {
        IMap? Map { get; set; }
        Dictionary<int, GameObject> GameObjects { get; }
        IDisposable ReadLock();
        IDisposable WriteLock();
        string GetSnapshot();
        string GetSnapshot(Region region);
        string GetSnapshot(MergedRegion region);
        IEnumerable<IGameObject> GetAllGameObjects();
    }
}
