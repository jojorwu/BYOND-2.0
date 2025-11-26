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
        }

        private class DreamTypeJson
        {
            public string? Path { get; set; }
            public string? Parent { get; set; }
            public Dictionary<string, JsonElement>? Vars { get; set; }
        }

        private readonly ObjectTypeManager _typeManager;

        public DreamMakerLoader(ObjectTypeManager typeManager)
        {
            _typeManager = typeManager;
        }

        public void Load(string jsonPath)
        {
            var jsonContent = File.ReadAllText(jsonPath);
            var compiledDream = JsonSerializer.Deserialize<DreamCompiledJson>(jsonContent);

            if (compiledDream?.Types == null)
            {
                Console.WriteLine("Warning: Compiled dream file contains no types.");
                return;
            }

            // First pass: Register all types to ensure they exist for parent linking
            foreach (var typeJson in compiledDream.Types)
            {
                if (string.IsNullOrEmpty(typeJson.Path)) continue;

                var newType = new ObjectType(typeJson.Path);
                if (!string.IsNullOrEmpty(typeJson.Parent))
                {
                    newType.ParentName = typeJson.Parent;
                }
                _typeManager.RegisterObjectType(newType);
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
                default:
                    // For more complex types like "new /icon('...')" or resource paths,
                    // OpenDream serializes them as objects. For now, we'll just store the raw text.
                    return element.ToString();
            }
        }
    }
}
