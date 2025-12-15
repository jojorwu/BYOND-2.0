using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Core.VM.Runtime;
using Core.VM.Types;
using DMCompiler.Json;
using Microsoft.Extensions.Logging;
using Shared;

namespace Core.Scripting.DM
{
    public class DmSystem : IThreadSupportingScriptSystem
    {
        private readonly ICompilerService _compiler;
        private readonly IObjectTypeManager _typeManager;
        private readonly IDreamMakerLoader _loader;
        private readonly IDreamVM _dreamVM;
        private readonly ILogger<DmSystem> _logger;

        public DmSystem(IObjectTypeManager typeManager, IDreamMakerLoader loader, ICompilerService compiler, IDreamVM dreamVM, ILogger<DmSystem> logger)
        {
            _typeManager = typeManager;
            _compiler = compiler;
            _loader = loader;
            _dreamVM = dreamVM;
            _logger = logger;
        }

        public void Initialize() { }

        public async Task LoadScripts(string rootDirectory)
        {
            var dmFiles = Directory.GetFiles(rootDirectory, "*.dm", SearchOption.AllDirectories).ToList();
            if (dmFiles.Count == 0) return;

            _logger.LogInformation($"[DM] Compiling {dmFiles.Count} files...");
            var (jsonPath, messages) = await Task.Run(() => _compiler.Compile(dmFiles));

            foreach (var msg in messages)
            {
                _logger.LogInformation($"[DM Compiler] {msg}");
            }

            if (jsonPath != null && File.Exists(jsonPath))
            {
                _logger.LogInformation("[DM] Loading compiled JSON...");
                await using var stream = File.OpenRead(jsonPath);
                var compiledJson = await JsonSerializer.DeserializeAsync<PublicDreamCompiledJson>(stream);

                if (compiledJson != null)
                {
                    _loader.Load(compiledJson);
                }
                else
                {
                    _logger.LogError("[DM] Error: Failed to deserialize compiled JSON.");
                }
            }
        }

        public IScriptThread? InvokeEvent(string eventName, params object[] args)
        {
            var thread = CreateThread(eventName);
            if (thread is not DreamThread dreamThread)
            {
                _logger.LogWarning($"[DM] Event '{eventName}' not found or thread is of incompatible type.");
                return null;
            }

            // Push arguments onto the stack in reverse order
            for (int i = args.Length - 1; i >= 0; i--)
            {
                dreamThread.Push(DreamValue.FromObject(args[i]));
            }

            _logger.LogDebug($"[DM] Invoked event '{eventName}'");
            return dreamThread;
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
