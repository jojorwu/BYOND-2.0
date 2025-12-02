using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json;
using System;

namespace Core
{
    public class GameState : IDisposable
    {
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        public Map? Map { get; set; }
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

        public string GetSnapshot()
        {
            using (ReadLock())
            {
                var snapshot = new
                {
                    Map,
                    GameObjects
                };
                return JsonConvert.SerializeObject(snapshot);
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
