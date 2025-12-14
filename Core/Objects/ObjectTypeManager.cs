using Shared;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Core.Objects
{
    public class ObjectTypeManager : IObjectTypeManager
    {
        private readonly ConcurrentDictionary<string, ObjectType> _objectTypes = new();
        private readonly ConcurrentDictionary<int, ObjectType> _objectTypesById = new();
        private readonly ConcurrentDictionary<string, List<ObjectType>> _unlinkedChildren = new();

        public void RegisterObjectType(ObjectType objectType)
        {
            if (!_objectTypes.TryAdd(objectType.Name, objectType) || !_objectTypesById.TryAdd(objectType.Id, objectType))
            {
                throw new System.InvalidOperationException($"Object type '{objectType.Name}' with ID {objectType.Id} is already registered.");
            }

            // Link to parent
            if (!string.IsNullOrEmpty(objectType.ParentName))
            {
                if (_objectTypes.TryGetValue(objectType.ParentName, out var parentType))
                {
                    objectType.Parent = parentType;
                    ValidateCircularDependencies(objectType);
                }
                else
                {
                    _unlinkedChildren.AddOrUpdate(objectType.ParentName,
                        new List<ObjectType> { objectType },
                        (key, list) => { list.Add(objectType); return list; });
                }
            }

            // Link any unlinked children to this new type
            if (_unlinkedChildren.TryRemove(objectType.Name, out var children))
            {
                foreach (var child in children)
                {
                    child.Parent = objectType;
                    ValidateCircularDependencies(child);
                }
            }
        }

        private void ValidateCircularDependencies(ObjectType objectType)
        {
            var slow = objectType;
            var fast = objectType;

            while (fast?.Parent != null && fast.Parent.Parent != null)
            {
                slow = slow.Parent!;
                fast = fast.Parent.Parent!;

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

        public ObjectType? GetObjectType(int id)
        {
            _objectTypesById.TryGetValue(id, out var objectType);
            return objectType;
        }

        public IEnumerable<ObjectType> GetAllObjectTypes()
        {
            return _objectTypes.Values;
        }

        public void Clear()
        {
            _objectTypes.Clear();
            _objectTypesById.Clear();
            _unlinkedChildren.Clear();
        }
    }
}
