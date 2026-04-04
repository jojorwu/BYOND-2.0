using Shared.Interfaces;
using Shared.Models;
using Shared.Attributes;

namespace Shared.Services;
    [EngineService(typeof(IObjectFactory))]
    public class ObjectFactory : IObjectFactory
    {
        private readonly IEntityRegistry _registry;

        public int EntityCount => _registry.EntityCount;

        public ObjectFactory(IEntityRegistry registry)
        {
            _registry = registry;
        }

        private int _maxObjectCount = 1000000;
        public int MaxObjectCount { get => _maxObjectCount; set => _maxObjectCount = value; }

        public GameObject Create(ObjectType objectType, long x = 0, long y = 0, long z = 0)
        {
            if (_registry.EntityCount >= _maxObjectCount)
                throw new System.InvalidOperationException("Maximum object count exceeded.");

            return _registry.CreateEntity(objectType, x, y, z);
        }

        public void Destroy(GameObject obj)
        {
            _registry.DestroyEntity(obj);
        }
    }
