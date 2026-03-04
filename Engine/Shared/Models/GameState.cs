using Shared;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Interfaces;

namespace Shared;
    public class GameState : IGameState, IEngineUpdateListener
    {
        private IMap? _map;
        public IMap? Map { get => Volatile.Read(ref _map); set => Volatile.Write(ref _map, value); }
        public SpatialGrid SpatialGrid { get; }
        public ConcurrentDictionary<long, GameObject> GameObjects { get; } = new ConcurrentDictionary<long, GameObject>();
        private readonly ConcurrentQueue<IGameObject> _dirtyObjects = new();
        private readonly IObjectFactory? _objectFactory;

        public GameState(SpatialGrid spatialGrid, IObjectFactory? objectFactory = null)
        {
            SpatialGrid = spatialGrid;
            _objectFactory = objectFactory;
        }

        public GameState() : this(new SpatialGrid(NullLogger<SpatialGrid>.Instance)) { } // For tests

        IDictionary<long, GameObject> IGameState.GameObjects => GameObjects;

        public void OnStateChanged(IGameObject obj) => _dirtyObjects.Enqueue(obj);
        public void OnPositionChanged(IGameObject obj, long oldX, long oldY, long oldZ) => SpatialGrid.Add(obj);

        public IDisposable ReadLock()
        {
            return new DisposableAction(() => { });
        }

        public IDisposable WriteLock()
        {
            return new DisposableAction(() => { });
        }

        public void Dispose()
        {
            SpatialGrid.Dispose();
        }

        public IEnumerable<IGameObject> GetAllGameObjects()
        {
            return GameObjects.Values;
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
            if (GameObjects.TryAdd(gameObject.Id, gameObject))
            {
                gameObject.SetUpdateListener(this);
                SpatialGrid.Add(gameObject);
                _dirtyObjects.Enqueue(gameObject);
            }
        }

        public void RemoveGameObject(GameObject gameObject)
        {
            if (GameObjects.TryRemove(gameObject.Id, out _))
            {
                gameObject.SetUpdateListener(null!);
                SpatialGrid.Remove(gameObject);

                _objectFactory?.Destroy(gameObject);
            }
        }

        public void UpdateGameObject(GameObject gameObject, long oldX, long oldY)
        {
            SpatialGrid.Update(gameObject, oldX, oldY);
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
