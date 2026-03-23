using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services;
    public class ObjectFactory : EngineService, IObjectFactory, IFreezable
    {
        private readonly IEntityRegistry _registry;
        private readonly IObjectTypeManager _typeManager;

        public ObjectFactory(IEntityRegistry registry, IObjectTypeManager typeManager)
        {
            _registry = registry;
            _typeManager = typeManager;
        }

        public GameObject Create(ObjectType objectType, long x = 0, long y = 0, long z = 0)
        {
            return _registry.CreateEntity(objectType, x, y, z);
        }

        public void Destroy(GameObject obj)
        {
            _registry.DestroyEntity(obj);
        }

        public void Freeze()
        {
            // Participates in freezing to ensure all registered types are finalized before extensive creation
        }
    }
