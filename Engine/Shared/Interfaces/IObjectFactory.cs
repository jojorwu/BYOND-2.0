using Shared.Interfaces;

namespace Shared.Interfaces;
    public interface IObjectFactory
    {
        GameObject Create(ObjectType objectType, long x = 0, long y = 0, long z = 0);
        void Destroy(GameObject obj);
    }
