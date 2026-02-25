using Shared;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Interfaces;

namespace Shared;
    public class GameState : IGameState
    {
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        public IMap? Map { get; set; }
        public SpatialGrid SpatialGrid { get; }
        public ConcurrentDictionary<int, GameObject> GameObjects { get; } = new ConcurrentDictionary<int, GameObject>();
        private readonly ConcurrentQueue<IGameObject> _dirtyObjects = new();
        private readonly IObjectFactory? _objectFactory;

        public GameState(SpatialGrid spatialGrid, IObjectFactory? objectFactory = null)
        {
            SpatialGrid = spatialGrid;
            _objectFactory = objectFactory;
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
            gameObject.PositionChanged += OnObjectPositionChanged;
            gameObject.StateChanged += OnObjectStateChanged;
            SpatialGrid.Add(gameObject);
            _dirtyObjects.Enqueue(gameObject);
        }

        public void RemoveGameObject(GameObject gameObject)
        {
            GameObjects.TryRemove(gameObject.Id, out _);
            gameObject.PositionChanged -= OnObjectPositionChanged;
            gameObject.StateChanged -= OnObjectStateChanged;
            SpatialGrid.Remove(gameObject);

            _objectFactory?.Destroy(gameObject);
        }

        public void UpdateGameObject(GameObject gameObject, int oldX, int oldY)
        {
            SpatialGrid.Update(gameObject, oldX, oldY);
        }

        private void OnObjectPositionChanged(GameObject obj, int oldX, int oldY, int oldZ)
        {
            UpdateGameObject(obj, oldX, oldY);
        }

        private void OnObjectStateChanged(IGameObject obj)
        {
            _dirtyObjects.Enqueue(obj);
        }

        public IEnumerable<IGameObject> GetDirtyObjects()
        {
            var list = new List<IGameObject>();
            while (_dirtyObjects.TryDequeue(out var obj))
            {
                list.Add(obj);
            }
            return list;
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
