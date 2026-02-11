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
using Core.Utils;

namespace Core
{
    public class CompiledJsonService : EngineService, ICompiledJsonService
    {
        private const int MaxLocalVariables = 1024;
        private readonly ILogger<CompiledJsonService>? _logger;
        private readonly IGameApi _gameApi;
        private readonly ITypeSystemPopulator _typeSystemPopulator;

        public CompiledJsonService(IGameApi gameApi, ITypeSystemPopulator? typeSystemPopulator = null, ILogger<CompiledJsonService>? logger = null)
        {
            _gameApi = gameApi;
            _typeSystemPopulator = typeSystemPopulator ?? new TypeSystemPopulator();
            _logger = logger;
        }

        public void PopulateState(ICompiledJson compiledJson, IDreamVM dreamVM, IObjectTypeManager typeManager)
        {
            if (compiledJson is not CompiledJson json)
                throw new ArgumentException("Invalid compiled json object", nameof(compiledJson));

            dreamVM.ObjectTypeManager = typeManager;
            dreamVM.GameApi = _gameApi;
            dreamVM.Initialize();

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
                    var localCount = procJson.Locals?.Count ?? 0;
                    if (localCount > MaxLocalVariables)
                        throw new Exception($"Procedure {procJson.Name} has too many local variables: {localCount}");

                    var newProc = new DreamProc(
                        procJson.Name,
                        bytecode,
                        arguments,
                        localCount
                    );

                    if (dreamVM is DreamVM vm2)
                    {
                        vm2.Context.AllProcs.Add(newProc);
                    }

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
                        newType.DefaultProperties[key] = JsonValueConverter.ConvertJsonElement(value);
                    }
                }

                if (newType.Name == "/list")
                {
                    dreamVM.ListType = newType;
                }

                if (newType.Name == "/world" && dreamVM is DreamVM vm6)
                {
                    vm6.Context.World = new DreamObject(newType);
                }
            }

            // Resolve parents
            for (int i = 0; i < json.Types.Length; i++)
            {
                var typeJson = json.Types[i];
                var currentType = objectTypes[i];
                if (typeJson.Parent.HasValue)
                {
                    var parentIdx = typeJson.Parent.Value;
                    if (parentIdx < 0 || parentIdx >= objectTypes.Length)
                        throw new Exception($"Invalid parent type index: {parentIdx}");
                    currentType.Parent = objectTypes[parentIdx];
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

            // Use the populator to flatten the type system
            _typeSystemPopulator.PopulateTypes(json.Types, objectTypes);

            // Load globals
            dreamVM.Globals.Clear();
            if (dreamVM is DreamVM vm3)
            {
                vm3.Context.GlobalNames.Clear();
            }

            if (json.Globals != null)
            {
                if (json.Globals.GlobalCount > 1000000)
                    throw new Exception($"Too many global variables: {json.Globals.GlobalCount}");

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
                    dreamVM.Globals[id] = DreamValue.FromObject(JsonValueConverter.ConvertJsonElement(value));
                }
            }
        }
    }
}
