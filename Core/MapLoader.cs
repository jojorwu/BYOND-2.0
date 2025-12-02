using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Core
{
    public class MapLoader
    {
        private readonly ObjectTypeManager _objectTypeManager;

        public MapLoader(ObjectTypeManager objectTypeManager)
        {
            _objectTypeManager = objectTypeManager;
        }

        public async Task<Map?> LoadMapAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            await using var stream = File.OpenRead(filePath);
            var mapData = await JsonSerializer.DeserializeAsync<MapData>(stream);

            if (mapData?.Turfs == null)
            {
                return null;
            }

            var map = new Map();
            foreach (var turfData in mapData.Turfs)
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
                            // Note: Deserializing object properties may require a custom converter
                            // for System.Text.Json depending on the actual types stored.
                            // For now, we assume they are simple primitives.
                            if (prop.Value is JsonElement element)
                            {
                                gameObject.Properties[prop.Key] = GetValueFromJsonElement(element);
                            }
                            else
                            {
                                gameObject.Properties[prop.Key] = prop.Value;
                            }
                        }
                        turf.Contents.Add(gameObject);
                    }
                }
                map.SetTurf(turfData.X, turfData.Y, turfData.Z, turf);
            }

            return map;
        }

        public async Task SaveMapAsync(Map map, string filePath)
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

            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, mapData, new JsonSerializerOptions { WriteIndented = true });
        }

        private object? GetValueFromJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int intValue)) return intValue;
                    if (element.TryGetDouble(out double doubleValue)) return doubleValue;
                    return element.GetDecimal();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                case JsonValueKind.Object:
                    var obj = new Dictionary<string, object?>();
                    foreach (var prop in element.EnumerateObject())
                    {
                        obj[prop.Name] = GetValueFromJsonElement(prop.Value);
                    }
                    return obj;
                case JsonValueKind.Array:
                    var list = new List<object?>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(GetValueFromJsonElement(item));
                    }
                    return list;
                default:
                    return element.ToString();
            }
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
