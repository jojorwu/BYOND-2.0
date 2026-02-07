using Shared;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using Core.VM.Procs;
using Core.VM.Runtime;
using Shared.Compiler;
using Core.Objects;
using Microsoft.Extensions.Logging;
using Shared.Services;

namespace Core
{
    public class CompiledJsonService : EngineService, ICompiledJsonService
    {
        private readonly ILogger<CompiledJsonService>? _logger;

        public CompiledJsonService(ILogger<CompiledJsonService>? logger = null)
        {
            _logger = logger;
        }

        public void PopulateState(ICompiledJson compiledJson, IDreamVM dreamVM, IObjectTypeManager typeManager)
        {
            if (compiledJson is not CompiledJson json)
                throw new ArgumentException("Invalid compiled json object", nameof(compiledJson));

            dreamVM.ObjectTypeManager = typeManager;
            StandardLibrary.Register(dreamVM);

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
            if (dreamVM is DreamVM vm)
            {
                vm.Context.AllProcs.Clear();
            }

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

                    if (dreamVM is DreamVM vm2)
                    {
                        vm2.Context.AllProcs.Add(newProc);
                    }

                    // Global procs are those where OwningTypeId is the root type (or some specific logic)
                    // For now, let's just use the name for global lookup if it's unique enough or handled elsewhere
                    dreamVM.Procs[newProc.Name] = newProc;
                }
            }

            // Load types and their properties
            var objectTypes = new ObjectType[json.Types.Length];
            for (int i = 0; i < json.Types.Length; i++)
            {
                var typeJson = json.Types[i];
                var newType = new ObjectType(i, typeJson.Path);
                objectTypes[i] = newType;
                typeManager.RegisterObjectType(newType);

                if (typeJson.Variables != null)
                {
                    foreach (var (key, value) in typeJson.Variables)
                    {
                        newType.DefaultProperties[key] = ConvertJsonElement(value);
                    }
                }

                if (newType.Name == "/list")
                {
                    dreamVM.ListType = newType;
                }
            }

            // Resolve parents and flatten variables
            for (int i = 0; i < json.Types.Length; i++)
            {
                var typeJson = json.Types[i];
                var currentType = objectTypes[i];
                if (typeJson.Parent.HasValue)
                {
                    currentType.Parent = objectTypes[typeJson.Parent.Value];
                }
            }

            // Map procs to types
            if (json.Procs != null && dreamVM is DreamVM vm5)
            {
                for (int i = 0; i < json.Procs.Length; i++)
                {
                    var procJson = json.Procs[i];
                    var proc = vm5.Context.AllProcs[i];
                    if (procJson.OwningTypeId >= 0 && procJson.OwningTypeId < objectTypes.Length)
                    {
                        objectTypes[procJson.OwningTypeId].Procs[proc.Name] = proc;
                    }
                }
            }

            for (int i = 0; i < json.Types.Length; i++)
            {
                var type = objectTypes[i];
                type.ClearCache();
                FlattenType(type, objectTypes, json.Types);
            }

            for (int i = 0; i < json.Types.Length; i++)
            {
                var type = objectTypes[i];
                FlattenProcs(type);
                type.FinalizeVariables();
            }

            // Load globals
            dreamVM.Globals.Clear();
            if (dreamVM is DreamVM vm3)
            {
                vm3.Context.GlobalNames.Clear();
            }

            if (json.Globals != null)
            {
                for (int i = 0; i < json.Globals.GlobalCount; i++)
                {
                    dreamVM.Globals.Add(DreamValue.Null);
                }

                if (json.Globals.Names != null && dreamVM is DreamVM vm4)
                {
                    for (int i = 0; i < json.Globals.Names.Count; i++)
                    {
                        vm4.Context.GlobalNames[json.Globals.Names[i]] = i;
                    }
                }

                foreach (var (id, value) in json.Globals.Globals)
                {
                    dreamVM.Globals[id] = DreamValue.FromObject(ConvertJsonElement(value));
                }
            }
        }

        private void FlattenProcs(ObjectType type)
        {
            if (type.FlattenedProcs.Count > 0 || type.Name == "/") return;

            if (type.Parent != null)
            {
                FlattenProcs(type.Parent);
                foreach (var (name, proc) in type.Parent.FlattenedProcs)
                {
                    type.FlattenedProcs[name] = proc;
                }
                foreach (var (name, proc) in type.Parent.Procs)
                {
                    type.FlattenedProcs[name] = proc;
                }
            }
        }

        private void FlattenType(ObjectType type, ObjectType[] allTypes, DreamTypeJson[] jsonTypes)
        {
            if (type.VariableNames.Count > 0 || type.Name == "/") return;

            if (type.Parent == null && type.Name != "/")
            {
                var typeJson = jsonTypes[type.Id];
                if (typeJson.Parent.HasValue)
                {
                    type.Parent = allTypes[typeJson.Parent.Value];
                }
            }

            if (type.Parent != null)
            {
                FlattenType(type.Parent, allTypes, jsonTypes);
                type.VariableNames.AddRange(type.Parent.VariableNames);
                type.FlattenedDefaultValues.AddRange(type.Parent.FlattenedDefaultValues);
            }

            foreach (var (name, value) in type.DefaultProperties)
            {
                int index = type.VariableNames.IndexOf(name);
                if (index != -1)
                {
                    type.FlattenedDefaultValues[index] = value;
                }
                else
                {
                    type.VariableNames.Add(name);
                    type.FlattenedDefaultValues.Add(value);
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
