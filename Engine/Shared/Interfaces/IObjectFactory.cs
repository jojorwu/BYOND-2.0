using Shared.Interfaces;

namespace Shared.Interfaces;
    public interface IObjectFactory
    {
        GameObject Create(ObjectType objectType, int x = 0, int y = 0, int z = 0);
        void Destroy(GameObject obj);
    }
