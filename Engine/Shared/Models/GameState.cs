using Shared;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;

namespace Shared;
    public class GameState : IGameState
    {
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        public IMap? Map { get; set; }
        public SpatialGrid SpatialGrid { get; }
        public ConcurrentDictionary<int, GameObject> GameObjects { get; } = new ConcurrentDictionary<int, GameObject>();

        public GameState(SpatialGrid spatialGrid)
        {
            SpatialGrid = spatialGrid;
        }

        public GameState() : this(new SpatialGrid(NullLogger<SpatialGrid>.Instance)) { } // For tests

        IDictionary<int, GameObject> IGameState.GameObjects => GameObjects;

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
            // SpatialGrid is shared, should it be disposed here?
            // In BYOND 2.0, GameState is the primary owner of the simulation grid.
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
            foreach (var obj in GameObjects.Values)
            {
                action(obj);
            }
        }

        public void AddGameObject(GameObject gameObject)
        {
            GameObjects.TryAdd(gameObject.Id, gameObject);
            using (WriteLock())
            {
                SpatialGrid.Add(gameObject);
            }
        }

        public void RemoveGameObject(GameObject gameObject)
        {
            GameObjects.TryRemove(gameObject.Id, out _);
            using (WriteLock())
            {
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
