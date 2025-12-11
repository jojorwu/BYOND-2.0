using System;
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
        private readonly ISet<string> _turfTypes = new HashSet<string> { "Wall", "Floor" }; // Easily extendable

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

            // First pass: Lay down all the turfs (walls, floors, etc.)
            foreach (var entity in entities.Where(e => _turfTypes.Contains(e.Id)))
            {
                var objectType = _objectTypeManager.GetObjectType(entity.Id);
                if (objectType == null)
                {
                    Console.WriteLine($"[SS14 Loader] Warning: ObjectType '{entity.Id}' not found. Skipping turf.");
                    continue;
                }

                if (!TryParsePosition(entity, out var x, out var y))
                    continue;

                // In this model, the turf itself is the object.
                var turf = new Turf(objectType.Id);
                var turfObject = new GameObject(objectType, x, y, 0);
                turf.Contents.Add(turfObject);
                map.SetTurf(x, y, 0, turf);
            }

            // Second pass: Place all other objects on top of the turfs
            foreach (var entity in entities.Where(e => !_turfTypes.Contains(e.Id)))
            {
                var objectType = _objectTypeManager.GetObjectType(entity.Id);
                if (objectType == null)
                {
                    Console.WriteLine($"[SS14 Loader] Warning: ObjectType '{entity.Id}' not found. Skipping object.");
                    continue;
                }

                if (!TryParsePosition(entity, out var x, out var y))
                    continue;

                var turf = map.GetTurf(x, y, 0);
                if (turf == null)
                {
                    Console.WriteLine($"[SS14 Loader] Warning: Trying to place object '{entity.Id}' at ({x},{y}) but no turf exists there. Skipping.");
                    continue;
                }

                var gameObject = new GameObject(objectType, x, y, 0);
                turf.Contents.Add(gameObject);
            }

            return map;
        }

        private bool TryParsePosition(Ss14Entity entity, out int x, out int y)
        {
            x = 0;
            y = 0;

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
                    return true;
                }
            }

            Console.WriteLine($"[SS14 Loader] Warning: Could not parse position for entity '{entity.Id}'.");
            return false;
        }
    }
}
