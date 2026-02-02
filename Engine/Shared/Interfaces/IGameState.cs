using System;
using System.Collections.Generic;

namespace Shared
{
    public interface IGameState : IDisposable
    {
        IMap? Map { get; set; }
        SpatialGrid SpatialGrid { get; }
        Dictionary<int, GameObject> GameObjects { get; }
        IDisposable ReadLock();
        IDisposable WriteLock();
        IEnumerable<IGameObject> GetAllGameObjects();
        void ForEachGameObject(Action<IGameObject> action);
        void AddGameObject(GameObject gameObject);
        void RemoveGameObject(GameObject gameObject);
        void UpdateGameObject(GameObject gameObject, int oldX, int oldY);
    }
}
