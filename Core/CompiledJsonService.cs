using Shared;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Core.VM.Procs;
using Core.VM.Runtime;
using Shared.Compiler;
using Core.Objects;

namespace Core
{
    public class CompiledJsonService : ICompiledJsonService
    {
        public void PopulateState(ICompiledJson compiledJson, IDreamVM dreamVM, IObjectTypeManager typeManager)
        {
            if (compiledJson is not CompiledJson json)
                throw new ArgumentException("Invalid compiled json object", nameof(compiledJson));

            // Load strings
            dreamVM.Strings.Clear();
            if (json.Strings != null)
            {
                foreach (var str in json.Strings)
                {
                    if (str != null)
                        dreamVM.Strings.Add(str);
                }
            }

            // Load procs
            dreamVM.Procs.Clear();
            if (json.Procs != null)
            {
                foreach (var procJson in json.Procs)
                {
                    var bytecode = procJson.Bytecode ?? Array.Empty<byte>();
                    var arguments = new string[procJson.Arguments?.Count ?? 0];
                    if (procJson.Arguments != null)
                    {
                        for (int i = 0; i < procJson.Arguments.Count; i++)
                        {
                            arguments[i] = procJson.Arguments[i].Name;
                        }
                    }
                    var newProc = new DreamProc(
                        procJson.Name,
                        bytecode,
                        arguments,
                        procJson.Locals?.Count ?? 0
                    );
                    dreamVM.Procs[newProc.Name] = newProc;
                }
            }

            // Load types and their properties
            for (int i = 0; i < json.Types.Length; i++)
            {
                var typeJson = json.Types[i];
                var newType = new ObjectType(i, typeJson.Path);

                if (typeJson.Parent.HasValue)
                {
                    var parentId = typeJson.Parent.Value;
                    if (parentId < json.Types.Length)
                    {
                        newType.ParentName = json.Types[parentId].Path;
                    }
                }
                typeManager.RegisterObjectType(newType);

                if (typeJson.Variables != null)
                {
                    foreach (var (key, value) in typeJson.Variables)
                    {
                        newType.DefaultProperties[key] = ConvertJsonElement(value);
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
                        return jsonElement.GetString() ?? string.Empty;
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
                            (JsonVariableType)typeElement.GetInt32() == JsonVariableType.Resource &&
                            jsonElement.TryGetProperty("resourcePath", out var pathElement) &&
                            pathElement.ValueKind == JsonValueKind.String)
                        {
                            return new DreamResource("resource", pathElement.GetString()!);
                        }
                        return jsonElement.ToString();
                    default:
                        return jsonElement.ToString();
                }
            }
            return element;
        }
    }
}
