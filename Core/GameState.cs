using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;

namespace Core
{
    public class GameState : IDisposable
    {
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private bool _isDirty = true;
        private string _snapshotCache = string.Empty;

        public Map? Map { get; set; }
        public Dictionary<int, GameObject> GameObjects { get; } = new Dictionary<int, GameObject>();

        public void SetDirty()
        {
            using (WriteLock())
            {
                _isDirty = true;
            }
        }

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
            _lock.EnterReadLock();
            try
            {
                if (!_isDirty)
                {
                    return _snapshotCache;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            _lock.EnterWriteLock();
            try
            {
                // Double-check the dirty flag, in case another thread has already updated the cache
                if (!_isDirty)
                {
                    return _snapshotCache;
                }

                var snapshotData = new
                {
                    Map,
                    GameObjects
                };
                _snapshotCache = JsonSerializer.Serialize(snapshotData);
                _isDirty = false;
                return _snapshotCache;
            }
            finally
            {
                _lock.ExitWriteLock();
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
