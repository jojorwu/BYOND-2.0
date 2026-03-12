using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services;
    public class ObjectFactory : IObjectFactory
    {
        private readonly IEntityRegistry _registry;

        public ObjectFactory(IEntityRegistry registry)
        {
            _registry = registry;
        }

        public GameObject Create(ObjectType objectType, long x = 0, long y = 0, long z = 0)
        {
            return _registry.CreateEntity(objectType, x, y, z);
        }

        public void Destroy(GameObject obj)
        {
            _registry.DestroyEntity(obj);
        }
    }
