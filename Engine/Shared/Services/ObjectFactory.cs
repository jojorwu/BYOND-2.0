using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services
{
    public class ObjectFactory : IObjectFactory
    {
        private readonly IObjectPool<GameObject> _pool;
        private readonly IComponentManager _componentManager;

        public ObjectFactory(IObjectPool<GameObject> pool, IComponentManager componentManager)
        {
            _pool = pool;
            _componentManager = componentManager;
        }

        public GameObject Create(ObjectType objectType, int x = 0, int y = 0, int z = 0)
        {
            var obj = _pool.Rent();
            obj.SetComponentManager(_componentManager);
            obj.Initialize(objectType, x, y, z);
            return obj;
        }

        public void Destroy(GameObject obj)
        {
            _pool.Return(obj);
        }
    }
}
