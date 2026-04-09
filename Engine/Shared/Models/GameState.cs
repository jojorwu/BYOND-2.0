using Shared;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Interfaces;

namespace Shared;
    public class GameState : Shared.Services.EngineService, IGameState, IEngineUpdateListener
    {
        public override IEnumerable<Type> Dependencies => new[] { typeof(IObjectFactory), typeof(Shared.Services.IArchetypeManager) };

        private IMap? _map;
        private readonly ReaderWriterLockSlim _worldLock = new(LockRecursionPolicy.SupportsRecursion);
        public IMap? Map { get => Volatile.Read(ref _map); set => Volatile.Write(ref _map, value); }
        public SpatialGrid SpatialGrid { get; }
        public ConcurrentDictionary<long, GameObject> GameObjects { get; } = new ConcurrentDictionary<long, GameObject>();
        private readonly ConcurrentQueue<IGameObject> _dirtyObjects = new();
        private readonly IObjectFactory? _objectFactory;
        public Shared.Services.IArchetypeManager ArchetypeManager { get; }

        public GameState(SpatialGrid spatialGrid, Shared.Services.IArchetypeManager archetypeManager, IObjectFactory? objectFactory = null)
        {
            SpatialGrid = spatialGrid;
            ArchetypeManager = archetypeManager;
            _objectFactory = objectFactory;
        }

        IDictionary<long, GameObject> IGameState.GameObjects => GameObjects;

        public void OnStateChanged(IGameObject obj) => _dirtyObjects.Enqueue(obj);
        public void OnPositionChanged(IGameObject obj, long oldX, long oldY, long oldZ) => SpatialGrid.Add(obj);

        public IDisposable ReadLock()
        {
            _worldLock.EnterReadLock();
            return new DisposableAction(() => _worldLock.ExitReadLock());
        }

        public IDisposable WriteLock()
        {
            _worldLock.EnterWriteLock();
            return new DisposableAction(() => _worldLock.ExitWriteLock());
        }

        public void Dispose()
        {
            SpatialGrid.Dispose();
            _worldLock.Dispose();
        }

        private sealed class DisposableAction : IDisposable
        {
            private readonly Action _action;
            public DisposableAction(Action action) => _action = action;
            public void Dispose() => _action();
        }

        public IEnumerable<IGameObject> GetAllGameObjects()
        {
            using (ReadLock())
            {
                return GameObjects.Values.ToList();
            }
        }

        public void ForEachGameObject(Action<IGameObject> action)
        {
            using (ReadLock())
            {
                foreach (var kvp in GameObjects)
                {
                    action(kvp.Value);
                }
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

        public IEnumerable<IGameObject> GetDirtyObjects() => _dirtyObjects;

        public void DrainDirtyObjects<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IGameState.IDirtyObjectVisitor, allows ref struct
        {
            while (_dirtyObjects.TryDequeue(out var obj))
            {
                visitor.Visit(obj);
            }
        }

        public void ClearDirtyObjects() => _dirtyObjects.Clear();

    }
