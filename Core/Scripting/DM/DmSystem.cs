using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Core.VM.Runtime;
using Core.VM.Types;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Compiler;

namespace Core.Scripting.DM
{
    public class DmSystem : IThreadSupportingScriptSystem
    {
        private readonly IObjectTypeManager _typeManager;
        private readonly IDreamMakerLoader _loader;
        private readonly IDreamVM _dreamVM;
        private readonly Lazy<IScriptHost> _scriptHostLazy;
        private readonly ILogger<DmSystem> _logger;
        private IScriptHost _scriptHost => _scriptHostLazy.Value;


        public DmSystem(IObjectTypeManager typeManager, IDreamMakerLoader loader, IDreamVM dreamVM, Lazy<IScriptHost> scriptHostLazy, ILogger<DmSystem> logger)
        {
            _typeManager = typeManager;
            _loader = loader;
            _dreamVM = dreamVM;
            _scriptHostLazy = scriptHostLazy;
            _logger = logger;
        }

        public void Initialize() { }

        public async Task LoadScripts(string rootDirectory)
        {
            var jsonPath = Path.Combine(rootDirectory, "project.compiled.json");
            if (!File.Exists(jsonPath))
            {
                _logger.LogWarning($"[DM] No compiled JSON file found at {jsonPath}.");
                return;
            }

            _logger.LogInformation($"[DM] Loading compiled JSON from {jsonPath}...");
            await using var stream = File.OpenRead(jsonPath);
            var compiledJson = await JsonSerializer.DeserializeAsync<CompiledJson>(stream, new JsonSerializerOptions() {
                PropertyNameCaseInsensitive = true
            });

            if (compiledJson != null)
            {
                _loader.Load(compiledJson);
            }
            else
            {
                _logger.LogError("[DM] Error: Failed to deserialize compiled JSON.");
            }
        }

        public void InvokeEvent(string eventName, params object[] args)
        {
            var thread = CreateThread(eventName);
            if (thread is not DreamThread dreamThread)
            {
                _logger.LogWarning($"[DM] Event '{eventName}' not found or thread is of incompatible type.");
                return;
            }

            // Push arguments onto the stack in reverse order
            for (int i = args.Length - 1; i >= 0; i--)
            {
                dreamThread.Push(DreamValue.FromObject(args[i]));
            }

            _scriptHost.AddThread(dreamThread);
            _logger.LogDebug($"[DM] Invoked event '{eventName}'");
        }

        public void Reload()
        {
            _typeManager.Clear();
            // LoadScripts will be called by the manager
        }

        public string? ExecuteString(string command)
        {
            // Not supported for DM scripts in this manner
            return null;
        }

        public IScriptThread? CreateThread(string procName, IGameObject? associatedObject = null)
        {
            return _dreamVM.CreateThread(procName, associatedObject);
        }
    }
}
