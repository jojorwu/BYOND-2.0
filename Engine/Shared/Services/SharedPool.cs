using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Shared.Interfaces;

namespace Shared.Services;
    /// <summary>
    /// A high-performance object pool that uses thread-local caches to minimize lock contention.
    /// </summary>
    /// <typeparam name="T">The type of objects to pool.</typeparam>
    public class SharedPool<T> : EngineService, IObjectPool<T> where T : class
    {
        private readonly Func<T> _factory;
        private readonly ConcurrentStack<T> _globalStack = new();
        private volatile int _globalCount;
        private const int LocalCapacity = 4096;
        private const int MaxGlobalCapacity = 1048576;

        private class LocalCache
        {
            public readonly T[] Items = new T[LocalCapacity];
            public int Count;
        }

        [ThreadStatic]
        private static LocalCache? _localCache;

        public override string Name => $"SharedPool<{typeof(T).Name}>";

        public SharedPool(Func<T> factory)
        {
            _factory = factory;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Rent()
        {
            var cache = _localCache;
            if (cache != null && cache.Count > 0)
            {
                int index = --cache.Count;
                ref var itemsRef = ref MemoryMarshal.GetArrayDataReference(cache.Items);
                T item = Unsafe.Add(ref itemsRef, index);
                Unsafe.Add(ref itemsRef, index) = null!;
                return item;
            }

            if (_globalStack.TryPop(out var globalObj))
            {
                Interlocked.Decrement(ref _globalCount);
                return globalObj;
            }

            return _factory();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Return(T obj)
        {
            if (obj is IPoolable poolable)
            {
                poolable.Reset();
            }

            var cache = _localCache ??= new LocalCache();

            if (cache.Count < LocalCapacity)
            {
                ref var itemsRef = ref MemoryMarshal.GetArrayDataReference(cache.Items);
                Unsafe.Add(ref itemsRef, cache.Count++) = obj;
            }
            else if (_globalCount < MaxGlobalCapacity)
            {
                _globalStack.Push(obj);
                Interlocked.Increment(ref _globalCount);
            }
        }

        public void Shrink()
        {
            // Prune global stack if it's large
            if (_globalCount > LocalCapacity)
            {
                // We keep some items to avoid immediate thrashing after shrink
                int targetCount = LocalCapacity / 2;
                while (_globalCount > targetCount && _globalStack.TryPop(out _))
                {
                    Interlocked.Decrement(ref _globalCount);
                }
            }
        }
    }
