namespace Shared;
    public interface IObjectApi
    {
        GameObject? CreateObject(int typeId, long x, long y, long z);
        GameObject? GetObject(long id);
        void DestroyObject(long id);
        void MoveObject(long id, long x, long y, long z);

        // Advanced queries
        System.Collections.Generic.List<GameObject> FindObjectsByType(string typePath);

        // Dynamic component management
        void AddComponent(long objectId, string componentType);
        void RemoveComponent(long objectId, string componentType);
        bool HasComponent(long objectId, string componentType);
    }
