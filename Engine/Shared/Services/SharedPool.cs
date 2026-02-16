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
        private const int LocalCapacity = 32;
        private const int MaxGlobalCapacity = 1024;

        private class LocalCache
        {
            public readonly T[] Items = new T[LocalCapacity];
            public int Count;
        }

        [ThreadStatic]
        private static LocalCache? _localCache;

        public SharedPool(Func<T> factory)
        {
            _factory = factory;
        }

        public T Rent()
        {
            var cache = _localCache;
            if (cache != null && cache.Count > 0)
            {
                int index = --cache.Count;
                T item = cache.Items[index];
                cache.Items[index] = null!;
                return item;
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

            var cache = _localCache ??= new LocalCache();

            if (cache.Count < LocalCapacity)
            {
                cache.Items[cache.Count++] = obj;
            }
            else if (_globalQueue.Count < MaxGlobalCapacity)
            {
                _globalQueue.Enqueue(obj);
            }
        }

        public void Shrink()
        {
            // Prune global queue to half its current size if it's large
            int count = _globalQueue.Count;
            if (count > LocalCapacity * 2)
            {
                for (int i = 0; i < count / 2; i++)
                {
                    _globalQueue.TryDequeue(out _);
                }
            }
        }
    }
}
