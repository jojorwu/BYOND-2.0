using Shared;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;

namespace Shared
{
    public class GameState : IGameState
    {
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        public IMap? Map { get; set; }
        public SpatialGrid SpatialGrid { get; } = new SpatialGrid();
        public Dictionary<int, GameObject> GameObjects { get; } = new Dictionary<int, GameObject>();

        public IDisposable ReadLock()
        {
            _lock.EnterReadLock();
            return new DisposableAction(() => _lock.ExitReadLock());
        }

        public IDisposable WriteLock()
        {
            _lock.EnterWriteLock();
            return new DisposableAction(() => _lock.ExitWriteLock());
        }

        public void Dispose()
        {
            _lock.Dispose();
            SpatialGrid.Dispose();
        }

        public IEnumerable<IGameObject> GetAllGameObjects()
        {
            using (ReadLock())
            {
                return new List<IGameObject>(GameObjects.Values);
            }
        }

        public void ForEachGameObject(Action<IGameObject> action)
        {
            using (ReadLock())
            {
                foreach (var obj in GameObjects.Values)
                {
                    action(obj);
                }
            }
        }

        public void AddGameObject(GameObject gameObject)
        {
            using (WriteLock())
            {
                GameObjects.Add(gameObject.Id, gameObject);
                SpatialGrid.Add(gameObject);
            }
        }

        public void RemoveGameObject(GameObject gameObject)
        {
            using (WriteLock())
            {
                GameObjects.Remove(gameObject.Id);
                SpatialGrid.Remove(gameObject);
            }
        }

        public void UpdateGameObject(GameObject gameObject, int oldX, int oldY)
        {
            using (WriteLock())
            {
                SpatialGrid.Update(gameObject, oldX, oldY);
            }
        }

        private sealed class DisposableAction : IDisposable
        {
            private readonly Action _action;

            public DisposableAction(Action action)
            {
                _action = action;
            }

            public void Dispose()
            {
                _action();
            }
        }
    }
}
