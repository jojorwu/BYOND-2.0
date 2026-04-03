using Shared;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Frozen;
using Shared.Services;
using Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace Shared.Services;
    public class ObjectTypeManager : EngineService, IObjectTypeManager, IFreezable
    {
        private readonly ConcurrentDictionary<string, ObjectType> _objectTypes = new();
        private readonly ConcurrentDictionary<int, ObjectType> _objectTypesById = new();
        private volatile FrozenDictionary<string, ObjectType> _frozenTypes = FrozenDictionary<string, ObjectType>.Empty;
        private volatile FrozenDictionary<int, ObjectType> _frozenTypesById = FrozenDictionary<int, ObjectType>.Empty;
        private readonly ConcurrentDictionary<string, List<ObjectType>> _unlinkedChildren = new();
        private readonly ILogger<ObjectTypeManager> _logger;

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
            var current = objectType.Parent;
            int depth = 0;
            const int MaxInheritanceDepth = 1000;

            while (current != null)
            {
                if (current == objectType)
                {
                    _logger.LogCritical("Circular dependency detected for type '{TypeName}' (ID: {TypeId}).", objectType.Name, objectType.Id);
                    throw new System.InvalidOperationException($"Circular dependency detected in object type '{objectType.Name}'.");
                }

                if (++depth > MaxInheritanceDepth)
                {
                    _logger.LogCritical("Max inheritance depth ({MaxDepth}) exceeded for type '{TypeName}'. Possible circular dependency.", MaxInheritanceDepth, objectType.Name);
                    throw new System.InvalidOperationException($"Inheritance depth too deep for type '{objectType.Name}'.");
                }

                current = current.Parent;
            }
        }

        public ObjectType? GetObjectType(string name)
        {
            if (_frozenTypes.TryGetValue(name, out var objectType)) return objectType;
            _objectTypes.TryGetValue(name, out objectType);
            return objectType;
        }

        public ObjectType? GetObjectType(int id)
        {
            if (_frozenTypesById.TryGetValue(id, out var objectType)) return objectType;
            _objectTypesById.TryGetValue(id, out objectType);
            return objectType;
        }

        public int TypeCount => _objectTypes.Count;

        public IEnumerable<ObjectType> GetAllObjectTypes()
        {
            return _objectTypes.Values;
        }

        public ObjectType GetTurfType()
        {
            return GetObjectType("/turf") ?? throw new System.InvalidOperationException("Base turf type '/turf' is not registered.");
        }

        private readonly IDiagnosticBus _diagnosticBus;

        public ObjectTypeManager(ILogger<ObjectTypeManager> logger, IDiagnosticBus diagnosticBus)
        {
            _logger = logger;
            _diagnosticBus = diagnosticBus;
        }

        public void Freeze()
        {
            int totalProcs = 0;
            int totalVars = 0;

            foreach (var type in _objectTypes.Values)
            {
                type.Freeze(_diagnosticBus);
                totalVars += type.VariableNames.Count;
                totalProcs += type.FlattenedProcs.Count;
                // Note: type.Freeze ensures _parentIds is populated
            }

            _frozenTypes = _objectTypes.ToFrozenDictionary();
            _frozenTypesById = _objectTypesById.ToFrozenDictionary();

            _diagnosticBus.Publish("ObjectTypeManager", "Type system frozen", DiagnosticSeverity.Info, m =>
            {
                m.Add("TypeCount", _frozenTypes.Count);
                m.Add("TotalVariableSlots", totalVars);
                m.Add("TotalProcDefinitions", totalProcs);
            });
        }

        public void Clear()
        {
            _objectTypes.Clear();
            _objectTypesById.Clear();
            _frozenTypes = FrozenDictionary<string, ObjectType>.Empty;
            _frozenTypesById = FrozenDictionary<int, ObjectType>.Empty;
            _unlinkedChildren.Clear();
        }
    }
