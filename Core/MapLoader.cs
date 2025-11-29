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

            var map = new Map();
            foreach(var turfData in mapData.Turfs)
            {
                var turf = new Turf(turfData.Id);
                foreach (var objData in turfData.Contents)
                {
                    var objectType = _objectTypeManager.GetObjectType(objData.TypeName);
                    if (objectType != null)
                    {
                        var gameObject = new GameObject(objectType, turfData.X, turfData.Y, turfData.Z);
                        foreach (var prop in objData.Properties)
                        {
                            gameObject.Properties[prop.Key] = prop.Value;
                        }
                        turf.Contents.Add(gameObject);
                    }
                }
                map.SetTurf(turfData.X, turfData.Y, turfData.Z, turf);
            }

            return map;
        }

        public void SaveMap(Map map, string filePath)
        {
            var mapData = new MapData();

            foreach (var z in map.GetZLevels())
            {
                foreach (var (chunkCoords, chunk) in map.GetChunks(z))
                {
                    for (int x = 0; x < Chunk.ChunkSize; x++)
                    {
                        for (int y = 0; y < Chunk.ChunkSize; y++)
                        {
                            var turf = chunk.GetTurf(x, y);
                            if (turf == null) continue;

                            mapData.Turfs.Add(new TurfData
                            {
                                X = chunkCoords.X * Chunk.ChunkSize + x,
                                Y = chunkCoords.Y * Chunk.ChunkSize + y,
                                Z = z,
                                Id = turf.Id,
                                Contents = turf.Contents.Select(obj => new GameObjectData
                                {
                                    TypeName = obj.ObjectType.Name,
                                    Properties = obj.Properties
                                }).ToList()
                            });
                        }
                    }
                }
            }


            var json = JsonConvert.SerializeObject(mapData, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        private class MapData
        {
            public List<TurfData> Turfs { get; set; } = new List<TurfData>();
        }

        private class TurfData
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Z { get; set; }
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
