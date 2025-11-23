using System.Collections.Generic;

namespace Core
{
    public class ObjectTypeManager
    {
        private readonly Dictionary<string, ObjectType> _objectTypes = new();

        public void RegisterObjectType(ObjectType objectType)
        {
            _objectTypes[objectType.Name] = objectType;
        }

        public ObjectType? GetObjectType(string name)
        {
            return _objectTypes.GetValueOrDefault(name);
        }

        public IEnumerable<ObjectType> GetAllObjectTypes()
        {
            return _objectTypes.Values;
        }
    }
}
