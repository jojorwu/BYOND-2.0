using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Shared.Interfaces;

namespace Shared.Services
{
    /// <summary>
    /// A high-performance object pool that uses thread-local caches to minimize lock contention.
    /// </summary>
    /// <typeparam name="T">The type of objects to pool.</typeparam>
    public class SharedPool<T> : IObjectPool<T> where T : class
    {
        private readonly Func<T> _factory;
        private readonly ConcurrentQueue<T> _globalQueue = new();
        private const int LocalCapacity = 16;

        [ThreadStatic]
        private static List<T>? _localCache;

        public SharedPool(Func<T> factory)
        {
            _factory = factory;
        }

        public T Rent()
        {
            _localCache ??= new List<T>(LocalCapacity);

            if (_localCache.Count > 0)
            {
                int lastIndex = _localCache.Count - 1;
                T obj = _localCache[lastIndex];
                _localCache.RemoveAt(lastIndex);
                return obj;
            }

            if (_globalQueue.TryDequeue(out var globalObj))
            {
                return globalObj;
            }

            return _factory();
        }

        public void Return(T obj)
        {
            if (obj is IPoolable poolable)
            {
                poolable.Reset();
            }

            _localCache ??= new List<T>(LocalCapacity);

            if (_localCache.Count < LocalCapacity)
            {
                _localCache.Add(obj);
            }
            else
            {
                _globalQueue.Enqueue(obj);
            }
        }
    }
}
