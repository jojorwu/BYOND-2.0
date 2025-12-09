using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Core.VM.Procs;
using Core.VM.Runtime;
using DMCompiler.Json;

namespace Core
{
    public class DreamMakerLoader
    {
        private readonly IObjectTypeManager _typeManager;
        private readonly IProject _project;
        private readonly DreamVM? _dreamVM;

        public DreamMakerLoader(IObjectTypeManager typeManager, IProject project, DreamVM? dreamVM = null)
        {
            _typeManager = typeManager;
            _project = project;
            _dreamVM = dreamVM;
        }

        public void Load(PublicDreamCompiledJson compiledJson)
        {
            if (_dreamVM != null)
            {
                // Load strings
                _dreamVM.Strings.Clear();
                if (compiledJson.Strings != null)
                {
                    foreach (var str in compiledJson.Strings)
                    {
                        if(str != null)
                            _dreamVM.Strings.Add(str);
                    }
                }

                // Load procs
                _dreamVM.Procs.Clear();
                if (compiledJson.Procs != null)
                {
                    foreach (var procJson in compiledJson.Procs)
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
                        _dreamVM.Procs[newProc.Name] = newProc;
                    }
                }
            }

            // Load types and their properties
            for (int i = 0; i < compiledJson.Types.Length; i++)
            {
                var typeJson = compiledJson.Types[i];
                var newType = new ObjectType(i, typeJson.Path);

                if (typeJson.Parent.HasValue)
                {
                    var parentId = typeJson.Parent.Value;
                    if (parentId < compiledJson.Types.Length)
                    {
                        newType.ParentName = compiledJson.Types[parentId].Path;
                    }
                }
                _typeManager.RegisterObjectType(newType);

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

        public DreamThread? CreateThread(string procName)
        {
            if (_dreamVM == null)
            {
                Console.WriteLine("Warning: DreamVM is not available. Cannot create a thread.");
                return null;
            }

            if (_dreamVM.Procs.TryGetValue(procName, out var proc))
            {
                return new DreamThread(proc, _dreamVM, 100000); // Using a default instruction limit for now
            }

            Console.WriteLine($"Warning: Could not find proc '{procName}' to create a thread.");
            return null;
        }
    }
}
