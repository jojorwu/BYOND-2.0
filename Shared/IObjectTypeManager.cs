using System.Collections.Generic;

namespace Shared
{
    public interface IObjectTypeManager
    {
        void RegisterObjectType(ObjectType objectType);
        ObjectType? GetObjectType(string name);
        IEnumerable<ObjectType> GetAllObjectTypes();
        void Clear();
    }
}
