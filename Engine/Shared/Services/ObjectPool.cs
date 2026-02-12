using System;
using System.Collections.Concurrent;
using Shared.Interfaces;

namespace Shared.Services
{
    public class ObjectPool<T> : IObjectPool<T> where T : class
    {
        private readonly ConcurrentStack<T> _pool = new();
        private readonly Func<T> _factory;

        public ObjectPool(Func<T> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public T Rent()
        {
            if (_pool.TryPop(out var obj))
            {
                return obj;
            }
            return _factory();
        }

        public void Return(T obj)
        {
            if (obj is IPoolable poolable)
            {
                poolable.Reset();
            }
            _pool.Push(obj);
        }
    }
}
