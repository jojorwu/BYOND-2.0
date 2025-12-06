using System.Collections.Generic;

namespace Core
{
    public interface IObjectTypeManager
    {
        void RegisterObjectType(ObjectType objectType);
        ObjectType? GetObjectType(string name);
        IEnumerable<ObjectType> GetAllObjectTypes();
        void Clear();
    }
}
