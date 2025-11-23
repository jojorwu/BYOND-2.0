using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Core
{
    public class MapLoader
    {
        private readonly ObjectTypeManager _objectTypeManager;

        public MapLoader(ObjectTypeManager objectTypeManager)
        {
            _objectTypeManager = objectTypeManager;
        }

        public Map? LoadMap(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = File.ReadAllText(filePath);
            var mapData = JsonConvert.DeserializeObject<MapData>(json);

            if (mapData?.Turfs == null)
            {
                return null;
            }

            var map = new Map(mapData.Width, mapData.Height, mapData.Depth);
            for (int z = 0; z < mapData.Depth; z++)
            {
                for (int y = 0; y < mapData.Height; y++)
                {
                    for (int x = 0; x < mapData.Width; x++)
                    {
                        var turfData = mapData.Turfs[z, y, x];
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
                                    gameObject.Properties[prop.Key] = prop.Value;
                                }
                                turf.Contents.Add(gameObject);
                            }
                        }
                        map.SetTurf(x, y, z, turf);
                    }
                }
            }

            return map;
        }

        public void SaveMap(Map map, string filePath)
        {
            var mapData = new MapData
            {
                Width = map.Width,
                Height = map.Height,
                Depth = map.Depth,
                Turfs = new TurfData[map.Depth, map.Height, map.Width]
            };

            for (int z = 0; z < map.Depth; z++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    for (int x = 0; x < map.Width; x++)
                    {
                        var turf = map.GetTurf(x, y, z);
                        if (turf == null) continue;

                        mapData.Turfs[z, y, x] = new TurfData
                        {
                            Id = turf.Id,
                            Contents = turf.Contents.Select(obj => new GameObjectData
                            {
                                TypeName = obj.ObjectType.Name,
                                Properties = obj.Properties
                            }).ToList()
                        };
                    }
                }
            }

            var json = JsonConvert.SerializeObject(mapData, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        private class MapData
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int Depth { get; set; }
            public TurfData[,,]? Turfs { get; set; }
        }

        private class TurfData
        {
            public int Id { get; set; }
            public List<GameObjectData> Contents { get; set; } = new List<GameObjectData>();
        }

        private class GameObjectData
        {
            public string TypeName { get; set; } = string.Empty;
            public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
        }
    }
}
