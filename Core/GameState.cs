using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json;

namespace Core
{
    public class GameState
    {
        private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);

        public Map? Map { get; set; }
        public Dictionary<int, GameObject> GameObjects { get; } = new Dictionary<int, GameObject>();

        public IDisposable ReadLock()
        {
            return new ReadLockDisposable(_lock);
        }

        public IDisposable WriteLock()
        {
            return new WriteLockDisposable(_lock);
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

        private readonly struct ReadLockDisposable : IDisposable
        {
            private readonly ReaderWriterLockSlim _lock;

            public ReadLockDisposable(ReaderWriterLockSlim rwLock)
            {
                _lock = rwLock;
                _lock.EnterReadLock();
            }

            public void Dispose()
            {
                _lock.ExitReadLock();
            }
        }

        private readonly struct WriteLockDisposable : IDisposable
        {
            private readonly ReaderWriterLockSlim _lock;

            public WriteLockDisposable(ReaderWriterLockSlim rwLock)
            {
                _lock = rwLock;
                _lock.EnterWriteLock();
            }

            public void Dispose()
            {
                _lock.ExitWriteLock();
            }
        }
    }
}
