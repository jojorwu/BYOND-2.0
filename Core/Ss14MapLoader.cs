using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shared;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Core.MapLoaders.Ss14.Model;

namespace Core
{
    public class Ss14MapLoader
    {
        private readonly IObjectTypeManager _objectTypeManager;

        public Ss14MapLoader(IObjectTypeManager objectTypeManager)
        {
            _objectTypeManager = objectTypeManager;
        }

        public IMap LoadMap(string filePath)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            var yaml = File.ReadAllText(filePath);
            var entities = deserializer.Deserialize<List<Ss14Entity>>(yaml);

            var map = new Map();
            foreach (var entity in entities)
            {
                if (string.IsNullOrEmpty(entity.Id))
                    continue;

                var objectType = _objectTypeManager.GetObjectType(entity.Id);
                if (objectType == null)
                {
                    var newId = _objectTypeManager.GetAllObjectTypes().Count() + 1;
                    objectType = new ObjectType(newId, entity.Id);
                    _objectTypeManager.RegisterObjectType(objectType);
                }

                var x = 0;
                var y = 0;

                var transformComponent = entity.Components
                    .FirstOrDefault(c => c.TryGetValue("type", out var type) && type.ToString() == "Transform");

                if (transformComponent != null && transformComponent.TryGetValue("pos", out var posObj) && posObj is string posStr)
                {
                    var posParts = posStr.Split(',');
                    if (posParts.Length == 2 &&
                        int.TryParse(posParts[0].Trim(), out var parsedX) &&
                        int.TryParse(posParts[1].Trim(), out var parsedY))
                    {
                        x = parsedX;
                        y = parsedY;
                    }
                }

                var turf = map.GetTurf(x, y, 0);
                if (turf == null)
                {
                    turf = new Turf(0); // Assuming default turf type
                    map.SetTurf(x, y, 0, turf);
                }

                var gameObject = new GameObject(objectType, x, y, 0);
                turf.Contents.Add(gameObject);
            }
            return map;
        }
    }
}
