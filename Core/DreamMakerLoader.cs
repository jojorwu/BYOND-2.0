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
    public class DreamMakerLoader : IDreamMakerLoader
    {
        private readonly IObjectTypeManager _typeManager;
        private readonly IProject _project;
        private readonly IDreamVM? _dreamVM;

        public DreamMakerLoader(IObjectTypeManager typeManager, IProject project, IDreamVM? dreamVM = null)
        {
            _typeManager = typeManager;
            _project = project;
            _dreamVM = dreamVM;
        }

        public void Load(IPublicDreamCompiledJson compiledJson)
        {
            if (compiledJson is not PublicDreamCompiledJson json)
                throw new ArgumentException("Invalid compiled json object", nameof(compiledJson));

            if (_dreamVM != null)
            {
                // Load strings
                _dreamVM.Strings.Clear();
                if (json.Strings != null)
                {
                    foreach (var str in json.Strings)
                    {
                        if(str != null)
                            _dreamVM.Strings.Add(str);
                    }
                }

                // Load procs
                _dreamVM.Procs.Clear();
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
                        _dreamVM.Procs[newProc.Name] = newProc;
                    }
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

        public IScriptThread? CreateThread(string procName)
        {
            if (_dreamVM is not DreamVM dreamVM)
            {
                Console.WriteLine("Warning: DreamVM is not available or of the wrong type. Cannot create a thread.");
                return null;
            }

            if (dreamVM.Procs.TryGetValue(procName, out var proc) && proc is DreamProc dreamProc)
            {
                return new DreamThread(dreamProc, dreamVM, 100000); // Using a default instruction limit for now
            }

            Console.WriteLine($"Warning: Could not find proc '{procName}' to create a thread.");
            return null;
        }
    }
}
