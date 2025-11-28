using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DMCompiler.Json;

namespace Core
{
    public class DreamMakerLoader
    {
        private readonly ObjectTypeManager _typeManager;
        private readonly Project _project;

        public DreamMakerLoader(ObjectTypeManager typeManager, Project project)
        {
            _typeManager = typeManager;
            _project = project;
        }

        public void Load(PublicDreamCompiledJson compiledJson)
        {
            // First pass: Register all types to ensure they exist for parent linking
            foreach (var typeJson in compiledJson.Types)
            {
                var newType = new ObjectType(typeJson.Path);

                if (typeJson.Parent.HasValue)
                {
                    var parentId = typeJson.Parent.Value;
                    if (parentId < compiledJson.Types.Length)
                    {
                        newType.ParentName = compiledJson.Types[parentId].Path;
                    }
                }
                _typeManager.RegisterObjectType(newType);
            }

            // Second pass: Set properties
            foreach (var typeJson in compiledJson.Types)
            {
                var objectType = _typeManager.GetObjectType(typeJson.Path);
                if (objectType == null)
                {
                    Console.WriteLine($"Warning: Could not find registered type {typeJson.Path} to set properties.");
                    continue;
                }

                if (typeJson.Variables != null)
                {
                    foreach (var (key, value) in typeJson.Variables)
                    {
                        objectType.DefaultProperties[key] = ConvertJsonElement(value);
                    }
                }
            }
        }

        private object? ConvertJsonElement(object? element)
        {
            if (element is JsonElement jsonElement)
            {
                switch (jsonElement.ValueKind)
                {
                    case JsonValueKind.String:
                        return jsonElement.GetString();
                    case JsonValueKind.Number:
                        if (jsonElement.TryGetInt32(out int intValue))
                            return intValue;
                        if (jsonElement.TryGetSingle(out float floatValue))
                            return floatValue;
                        return jsonElement.GetDouble();
                    case JsonValueKind.True:
                        return true;
                    case JsonValueKind.False:
                        return false;
                    case JsonValueKind.Null:
                        return null;
                    case JsonValueKind.Object:
                        if (jsonElement.TryGetProperty("type", out var typeElement) &&
                            typeElement.ValueKind == JsonValueKind.Number &&
                            (PublicJsonVariableType)typeElement.GetInt32() == PublicJsonVariableType.Resource &&
                            jsonElement.TryGetProperty("resourcePath", out var pathElement) &&
                            pathElement.ValueKind == JsonValueKind.String)
                        {
                            return new DreamResource("resource", pathElement.GetString()!);
                        }
                        // Fallback for other object types
                        return jsonElement.ToString();
                    default:
                        return jsonElement.ToString();
                }
            }
            return element;
        }
    }
}
