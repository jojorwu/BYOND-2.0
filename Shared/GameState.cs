using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace Shared
{
    public class GameState : IGameState
    {
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        public IMap? Map { get; set; }
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

        public virtual string GetSnapshot()
        {
            using (ReadLock())
            {
                var snapshot = new
                {
                    Map,
                    GameObjects
                };
                return JsonSerializer.Serialize(snapshot);
            }
        }

        public string GetSnapshot(Region region)
        {
            using (ReadLock())
            {
                var regionChunks = region.GetChunks().ToHashSet();
                var regionGameObjects = region.GetGameObjects()
                    .OfType<GameObject>()
                    .ToDictionary(go => go.Id);

                var snapshot = new
                {
                    Chunks = regionChunks,
                    GameObjects = regionGameObjects
                };
                return JsonSerializer.Serialize(snapshot);
            }
        }

        public void Dispose()
        {
            _lock.Dispose();
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
