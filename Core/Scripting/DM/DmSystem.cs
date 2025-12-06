using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Core.VM.Runtime;
using Core.VM.Types;
using DMCompiler.Json;
using Shared;

namespace Core.Scripting.DM
{
    public class DmSystem : IScriptSystem
    {
        private readonly OpenDreamCompilerService _compiler;
        private readonly ObjectTypeManager _typeManager;
        private readonly DreamMakerLoader _loader;
        private readonly Func<IScriptHost> _scriptHostFactory;
        private IScriptHost _scriptHost => _scriptHostFactory();


        public DmSystem(ObjectTypeManager typeManager, IProject project, DreamVM dreamVM, Func<IScriptHost> scriptHostFactory)
        {
            _typeManager = typeManager;
            _compiler = new OpenDreamCompilerService(project);
            _loader = new DreamMakerLoader(_typeManager, project, dreamVM);
            _scriptHostFactory = scriptHostFactory;
        }

        public void Initialize() { }

        public async Task LoadScripts(string rootDirectory)
        {
            var dmFiles = Directory.GetFiles(rootDirectory, "*.dm", SearchOption.AllDirectories).ToList();
            if (dmFiles.Count == 0) return;

            Console.WriteLine($"[DM] Compiling {dmFiles.Count} files...");
            var (jsonPath, messages) = await Task.Run(() => _compiler.Compile(dmFiles));

            foreach (var msg in messages)
            {
                Console.WriteLine($"[DM Compiler] {msg}");
            }

            if (jsonPath != null && File.Exists(jsonPath))
            {
                Console.WriteLine("[DM] Loading compiled JSON...");
                await using var stream = File.OpenRead(jsonPath);
                var compiledJson = await JsonSerializer.DeserializeAsync<PublicDreamCompiledJson>(stream);

                if (compiledJson != null)
                {
                    _loader.Load(compiledJson);
                }
                else
                {
                    Console.WriteLine("[DM] Error: Failed to deserialize compiled JSON.");
                }
            }
        }

        public void InvokeEvent(string eventName, params object[] args)
        {
            var thread = CreateThread(eventName);
            if (thread == null)
            {
                Console.WriteLine($"[DM] Event '{eventName}' not found.");
                return;
            }

            // Push arguments onto the stack in reverse order
            for (int i = args.Length - 1; i >= 0; i--)
            {
                thread.Push(DreamValue.FromObject(args[i]));
            }

            _scriptHost.AddThread(thread);
            Console.WriteLine($"[DM] Invoked event '{eventName}'");
        }

        public void Reload()
        {
            _typeManager.Clear();
            // LoadScripts будет вызван менеджером
        }

        public void ExecuteString(string command)
        {
            // Not supported for DM scripts in this manner
        }

        public DreamThread? CreateThread(string procName)
        {
            return _loader.CreateThread(procName);
        }
    }
}
