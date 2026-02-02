using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Core.Maps
{
    public class MapLoader : IMapLoader
    {
        private readonly IObjectTypeManager _objectTypeManager;

        public MapLoader(IObjectTypeManager objectTypeManager)
        {
            _objectTypeManager = objectTypeManager;
        }

        public async Task<IMap?> LoadMapAsync(string filePath)
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
                var turfType = _objectTypeManager.GetTurfType();
                var turf = new Turf(turfType, turfData.X, turfData.Y, turfData.Z);
                turf.Id = turfData.Id;
                foreach (var objData in turfData.Contents)
                {
                    var objectType = _objectTypeManager.GetObjectType(objData.TypeName);
                    if (objectType != null)
                    {
                        var gameObject = new GameObject(objectType, turfData.X, turfData.Y, turfData.Z);
                        foreach (var prop in objData.Properties)
                        {
                            if (prop.Value is JsonElement element)
                            {
                                gameObject.SetVariable(prop.Key, DreamValue.FromObject(GetValueFromJsonElement(element)));
                            }
                            else
                            {
                                gameObject.SetVariable(prop.Key, DreamValue.FromObject(prop.Value));
                            }
                        }
                        turf.AddContent(gameObject);
                    }
                }
                map.SetTurf(turfData.X, turfData.Y, turfData.Z, turf);
            }

            return map;
        }

        public async Task SaveMapAsync(IMap map, string filePath)
        {
            await using var stream = File.Create(filePath);
            await using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

            writer.WriteStartObject();
            writer.WriteStartArray("Turfs");

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

                            writer.WriteStartObject();
                            writer.WriteNumber("X", chunkCoords.X * Chunk.ChunkSize + x);
                            writer.WriteNumber("Y", chunkCoords.Y * Chunk.ChunkSize + y);
                            writer.WriteNumber("Z", z);
                            writer.WriteNumber("Id", turf.Id);
                            writer.WriteStartArray("Contents");

                            foreach (var obj in turf.Contents)
                            {
                                writer.WriteStartObject();
                                writer.WriteString("TypeName", obj.ObjectType.Name);
                                writer.WritePropertyName("Properties");

                                var props = new Dictionary<string, object?>();
                                for (int i = 0; i < obj.ObjectType.VariableNames.Count; i++)
                                {
                                    var varName = obj.ObjectType.VariableNames[i];
                                    var val = obj.GetVariable(i);

                                    // We only save it if it's different from the default value
                                    // To keep it simple for now, we save everything
                                    if (val.TryGetValue(out float f)) props[varName] = f;
                                    else if (val.TryGetValue(out string? s)) props[varName] = s;
                                    else if (val.Type == DreamValueType.Null) props[varName] = null;
                                    else props[varName] = val.ToString();
                                }

                                JsonSerializer.Serialize(writer, props);
                                writer.WriteEndObject();
                            }

                            writer.WriteEndArray();
                            writer.WriteEndObject();
                        }
                    }
                }
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            await writer.FlushAsync();
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
            public Dictionary<string, object?> Properties { get; set; } = new Dictionary<string, object?>();
        }
    }
}
