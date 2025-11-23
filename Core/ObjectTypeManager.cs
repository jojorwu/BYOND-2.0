using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Core
{
    public class ObjectTypeManager
    {
        private const string TypesFileName = "types.json";
        private readonly ConcurrentDictionary<string, ObjectType> _objectTypes = new();
        private readonly Project _project;

        public ObjectTypeManager(Project project)
        {
            _project = project;
        }

        public void RegisterObjectType(ObjectType objectType)
        {
            if (_objectTypes.TryAdd(objectType.Name, objectType))
            {
                // Link this type to its parent if the parent is already registered.
                if (objectType.ParentName != null)
                {
                    objectType.Parent = GetObjectType(objectType.ParentName);
                }

                // Link any existing children to this type if this is their parent.
                foreach (var childType in _objectTypes.Values)
                {
                    if (childType.ParentName == objectType.Name)
                    {
                        childType.Parent = objectType;
                    }
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
            return _objectTypes.Values.OrderBy(t => t.Name);
        }

        public void SaveTypes()
        {
            var path = _project.GetFullPath(TypesFileName);
            var json = JsonConvert.SerializeObject(_objectTypes.Values, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        public void LoadTypes()
        {
            var path = _project.GetFullPath(TypesFileName);
            if (!File.Exists(path))
            {
                return;
            }

            var json = File.ReadAllText(path);
            var types = JsonConvert.DeserializeObject<List<ObjectType>>(json);
            if (types == null)
            {
                return;
            }

            _objectTypes.Clear();
            foreach (var type in types)
            {
                RegisterObjectType(type);
            }
        }
    }
}
