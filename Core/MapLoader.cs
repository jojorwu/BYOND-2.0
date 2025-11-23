using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;

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
            var mapData = JsonConvert.DeserializeObject<MapData>(json);

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
            });

            return map;
        }

        public void SaveMap(Map map, string filePath)
        {
            var fullPath = _project.GetFullPath(filePath);

            var turfData = new ConcurrentDictionary<Vector3D, TurfData>();
            Parallel.ForEach(map.GetAllTurfs(), kvp =>
            {
                var turf = kvp.Value;
                turfData[kvp.Key] = new TurfData
                {
                    Id = turf.Id,
                    Contents = turf.Contents.Select(obj => new GameObjectData
                    {
                        TypeName = obj.ObjectType.Name,
                        Properties = obj.Properties
                    }).ToList()
                };
            });

            var mapData = new MapData
            {
                Width = map.Width,
                Height = map.Height,
                Depth = map.Depth,
                Turfs = new TurfData[map.Depth, map.Height, map.Width]
            };

            foreach (var kvp in turfData)
            {
                mapData.Turfs[kvp.Key.Z, kvp.Key.Y, kvp.Key.X] = kvp.Value;
            }

            var json = JsonConvert.SerializeObject(mapData, Formatting.Indented);
            File.WriteAllText(fullPath, json);
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
