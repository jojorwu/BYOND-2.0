using System.Collections.Generic;

namespace Shared
{
    public interface IObjectTypeManager
    {
        void RegisterObjectType(ObjectType objectType);
        ObjectType? GetObjectType(string name);
        ObjectType? GetObjectType(int id);
        IEnumerable<ObjectType> GetAllObjectTypes();
        ObjectType GetTurfType();
        void Clear();
    }
}
