using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Core
{
    public class ObjectTypeManager
    {
        private readonly ConcurrentDictionary<string, ObjectType> _objectTypes = new();

        public void RegisterObjectType(ObjectType objectType)
        {
            if (_objectTypes.ContainsKey(objectType.Name))
            {
                throw new System.InvalidOperationException($"Object type '{objectType.Name}' is already registered.");
            }

            _objectTypes[objectType.Name] = objectType;

            // Link this object type to its parent if the parent is already registered.
            if (!string.IsNullOrEmpty(objectType.ParentName))
            {
                if (_objectTypes.TryGetValue(objectType.ParentName, out var parentType))
                {
                    objectType.Parent = parentType;
                    ValidateCircularDependencies(objectType);
                }
            }

            // Link any existing object types that are children of this new type.
            foreach (var childType in _objectTypes.Values)
            {
                if (childType.ParentName == objectType.Name)
                {
                    childType.Parent = objectType;
                    ValidateCircularDependencies(childType);
                }
            }
        }

        private void ValidateCircularDependencies(ObjectType objectType)
        {
            var slow = objectType;
            var fast = objectType;

            while (fast?.Parent != null && fast.Parent.Parent != null)
            {
                slow = slow.Parent;
                fast = fast.Parent.Parent;

                if (slow == fast)
                {
                    throw new System.InvalidOperationException($"Circular dependency detected in object type '{objectType.Name}'.");
                }
            }
        }

        public ObjectType? GetObjectType(string name)
        {
            _objectTypes.TryGetValue(name, out var objectType);
            return objectType;
        }

        public IEnumerable<ObjectType> GetAllObjectTypes()
        {
            return _objectTypes.Values;
        }
    }
}
