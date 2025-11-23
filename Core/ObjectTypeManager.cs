using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Core
{
    public class ObjectTypeManager
    {
        private const string TypesFilePath = "types.json";
        private readonly ConcurrentDictionary<string, ObjectType> _objectTypes = new();

        public void RegisterObjectType(ObjectType objectType)
        {
            _objectTypes.TryAdd(objectType.Name, objectType);
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
            var json = JsonConvert.SerializeObject(_objectTypes.Values, Formatting.Indented);
            File.WriteAllText(TypesFilePath, json);
        }

        public void LoadTypes()
        {
            if (!File.Exists(TypesFilePath))
            {
                return;
            }

            var json = File.ReadAllText(TypesFilePath);
            var types = JsonConvert.DeserializeObject<List<ObjectType>>(json);
            if (types == null)
            {
                return;
            }

            _objectTypes.Clear();
            foreach (var type in types)
            {
                _objectTypes.TryAdd(type.Name, type);
            }
        }
    }
}
