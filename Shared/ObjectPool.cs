using System;
using System.Collections.Concurrent;

namespace Shared
{
    public class ObjectPool<T> where T : class
    {
        private readonly ConcurrentBag<T> _objects;
        private readonly Func<T> _objectFactory;

        public ObjectPool(Func<T> objectFactory)
        {
            _objectFactory = objectFactory ?? throw new ArgumentNullException(nameof(objectFactory));
            _objects = new ConcurrentBag<T>();
        }

        public T Get()
        {
            if (_objects.TryTake(out var item))
                return item;

            return _objectFactory();
        }

        public void Return(T item)
        {
            _objects.Add(item);
        }
    }
}
