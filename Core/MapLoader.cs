using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System;

namespace Core
{
    public class MapLoader
    {
        private readonly ObjectTypeManager _objectTypeManager;
        private readonly Project _project;

        public MapLoader(ObjectTypeManager objectTypeManager, Project project)
        {
            _objectTypeManager = objectTypeManager;
            _project = project;
        }

        public Map? LoadMap(string filePath)
        {
            var fullPath = _project.GetFullPath(filePath);

            if (!File.Exists(fullPath))
            {
                return null;
            }

            var json = File.ReadAllText(fullPath);
            var mapData = (MapData?)JsonSerializer.Deserialize(json, typeof(MapData), JsonContext.Default);

            if (mapData?.Turfs == null)
            {
                return null;
            }

            var map = new Map(mapData.Width, mapData.Height, mapData.Depth);
            Parallel.For(0, mapData.Depth, z =>
            {
                for (int y = 0; y < mapData.Height; y++)
                {
                    for (int x = 0; x < mapData.Width; x++)
                    {
                        var index = z * mapData.Width * mapData.Height + y * mapData.Width + x;
                        var turfData = mapData.Turfs[index];
                        if (turfData == null) continue;

                        var turf = new Turf(turfData.Id);
                        foreach (var objData in turfData.Contents)
                        {
                            var objectType = _objectTypeManager.GetObjectType(objData.TypeName);
                            if (objectType != null)
                            {
                                var gameObject = new GameObject(objectType, x, y, z);
                                foreach (var prop in objData.Properties)
                                {
                                    gameObject.Properties[prop.Key] = ConvertJsonElement(prop.Value);
                                }
                                turf.Contents.Add(gameObject);
                            }
                        }
                        map.SetTurf(x, y, z, turf);
                    }
                }
            });

            return map;
        }

        private object ConvertJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString()!;
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out long l))
                    {
                        return l;
                    }
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null!;
                default:
                    throw new NotSupportedException($"Unsupported JsonElement type: {element.ValueKind}");
            }
        }


        public void SaveMap(Map map, string filePath)
        {
            var fullPath = _project.GetFullPath(filePath);

            var mapData = new MapData
            {
                Width = map.Width,
                Height = map.Height,
                Depth = map.Depth,
                Turfs = new List<TurfData?>(new TurfData[map.Width * map.Height * map.Depth])
            };

            Parallel.ForEach(map.GetAllTurfs(), kvp =>
            {
                var turf = kvp.Value;
                var index = kvp.Key.Z * map.Width * map.Height + kvp.Key.Y * map.Width + kvp.Key.X;
                mapData.Turfs[index] = new TurfData
                {
                    Id = turf.Id,
                    Contents = turf.Contents.Select(obj =>
                    {
                        var gameObjectData = new GameObjectData
                        {
                            TypeName = obj.ObjectType.Name,
                            Properties = new Dictionary<string, JsonElement>()
                        };
                        foreach (var prop in obj.Properties)
                        {
                            gameObjectData.Properties[prop.Key] = JsonSerializer.SerializeToElement(prop.Value);
                        }
                        return gameObjectData;
                    }).ToList()
                };
            });

            var json = JsonSerializer.Serialize(mapData, typeof(MapData), JsonContext.Default);
            File.WriteAllText(fullPath, json);
        }
    }
}
