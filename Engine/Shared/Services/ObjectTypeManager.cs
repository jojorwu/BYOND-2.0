using Shared;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Shared.Services;
using Microsoft.Extensions.Logging;

namespace Shared.Services
{
    public class ObjectTypeManager : EngineService, IObjectTypeManager
    {
        private readonly ConcurrentDictionary<string, ObjectType> _objectTypes = new();
        private readonly ConcurrentDictionary<int, ObjectType> _objectTypesById = new();
        private readonly ConcurrentDictionary<string, List<ObjectType>> _unlinkedChildren = new();
        private readonly ILogger<ObjectTypeManager> _logger;

        public ObjectTypeManager(ILogger<ObjectTypeManager> logger)
        {
            _logger = logger;
        }

        public void RegisterObjectType(ObjectType objectType)
        {
            if (!_objectTypes.TryAdd(objectType.Name, objectType) || !_objectTypesById.TryAdd(objectType.Id, objectType))
            {
                _logger.LogError("Object type '{ObjectTypeName}' with ID {ObjectTypeId} is already registered.", objectType.Name, objectType.Id);
                throw new System.InvalidOperationException($"Object type '{objectType.Name}' with ID {objectType.Id} is already registered.");
            }

            _logger.LogTrace("Registered object type: {ObjectTypeName} ({ObjectTypeId})", objectType.Name, objectType.Id);

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

        public ObjectType GetTurfType()
        {
            return GetObjectType("/turf") ?? throw new System.InvalidOperationException("Base turf type '/turf' is not registered.");
        }

        public void Clear()
        {
            _objectTypes.Clear();
            _objectTypesById.Clear();
            _unlinkedChildren.Clear();
        }
    }
}
