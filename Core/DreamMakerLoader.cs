using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Core
{
    public class DreamMakerLoader
    {
        private class DreamCompiledJson
        {
            public List<DreamTypeJson>? Types { get; set; }
            public List<JsonElement>? Maps { get; set; }
        }

        private class DreamTypeJson
        {
            public string? Path { get; set; }
            public JsonElement? Parent { get; set; }
            public Dictionary<string, JsonElement>? Vars { get; set; }
        }

        private readonly ObjectTypeManager _typeManager;

        public DreamMakerLoader(ObjectTypeManager typeManager)
        {
            _typeManager = typeManager;
        }

        public Map? Load(string jsonPath)
        {
            var jsonContent = File.ReadAllText(jsonPath);
            var compiledDream = JsonSerializer.Deserialize<DreamCompiledJson>(jsonContent);

            if (compiledDream?.Types == null)
            {
                Console.WriteLine("Warning: Compiled dream file contains no types.");
                return null;
            }

            var typeIdMap = new Dictionary<int, ObjectType>();

            // First pass: Register all types to ensure they exist for parent linking
            for (var i = 0; i < compiledDream.Types.Count; i++)
            {
                var typeJson = compiledDream.Types[i];
                if (string.IsNullOrEmpty(typeJson.Path)) continue;

                var newType = new ObjectType(typeJson.Path);

                if (typeJson.Parent.HasValue)
                {
                    var parentElement = typeJson.Parent.Value;
                    if (parentElement.ValueKind == JsonValueKind.String)
                    {
                        newType.ParentName = parentElement.GetString();
                    }
                    else if (parentElement.ValueKind == JsonValueKind.Number)
                    {
                        var parentId = parentElement.GetInt32();
                        if (parentId < compiledDream.Types.Count)
                        {
                            newType.ParentName = compiledDream.Types[parentId].Path;
                        }
                    }
                }
                _typeManager.RegisterObjectType(newType);
                var registeredType = _typeManager.GetObjectType(newType.Name);
                if(registeredType != null)
                    typeIdMap[i] = registeredType;
            }

            // Second pass: Set properties
            foreach (var typeJson in compiledDream.Types)
            {
                if (string.IsNullOrEmpty(typeJson.Path) || typeJson.Vars == null) continue;

                var objectType = _typeManager.GetObjectType(typeJson.Path);
                if (objectType == null)
                {
                    Console.WriteLine($"Warning: Could not find registered type {typeJson.Path} to set properties.");
                    continue;
                }

                foreach (var (key, value) in typeJson.Vars)
                {
                    objectType.DefaultProperties[key] = ConvertJsonElement(value);
                }
            }

            if (compiledDream.Maps != null && compiledDream.Maps.Count > 0)
            {
                var dmmLoader = new DmmLoader(_typeManager, typeIdMap);
                return dmmLoader.LoadMap(compiledDream.Maps[0]);
            }

            return null;
        }

        private object? ConvertJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int intValue))
                        return intValue;
                    if (element.TryGetSingle(out float floatValue))
                        return floatValue;
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                case JsonValueKind.Object:
                    if (element.TryGetProperty("type", out var typeElement) &&
                        typeElement.ValueKind == JsonValueKind.String &&
                        element.TryGetProperty("path", out var pathElement) &&
                        pathElement.ValueKind == JsonValueKind.String)
                    {
                        return new DreamResource(typeElement.GetString()!, pathElement.GetString()!);
                    }
                    // Fallback for other object types
                    return element.ToString();
                default:
                    return element.ToString();
            }
        }
    }
}
