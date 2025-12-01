using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Core.VM.Runtime;
using DMCompiler.Json;

namespace Core.Scripting.DM
{
    public class DmSystem : IScriptSystem
    {
        private readonly OpenDreamCompilerService _compiler;
        private readonly ObjectTypeManager _typeManager;
        private readonly DreamMakerLoader _loader;

        public DmSystem(ObjectTypeManager typeManager, Project project, DreamVM dreamVM)
        {
            _typeManager = typeManager;
            _compiler = new OpenDreamCompilerService(project);
            _loader = new DreamMakerLoader(_typeManager, project, dreamVM);
        }

        public void Initialize() { }

        public Task LoadScripts(string rootDirectory)
        {
            var dmFiles = Directory.GetFiles(rootDirectory, "*.dm", SearchOption.AllDirectories).ToList();
            if (dmFiles.Count == 0) return Task.CompletedTask;

            Console.WriteLine($"[DM] Compiling {dmFiles.Count} files...");
            var (jsonPath, messages) = _compiler.Compile(dmFiles);

            foreach (var msg in messages)
            {
                Console.WriteLine($"[DM Compiler] {msg}");
            }

            if (jsonPath != null && File.Exists(jsonPath))
            {
                Console.WriteLine("[DM] Loading compiled JSON...");
                var json = File.ReadAllText(jsonPath);
                var compiledJson = JsonSerializer.Deserialize<PublicDreamCompiledJson>(json);
                if (compiledJson != null)
                {
                    _loader.Load(compiledJson);
                }
            }
            return Task.CompletedTask;
        }

        public void InvokeEvent(string eventName, params object[] args)
        {
            // Здесь будет вызов DreamProc через VM
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
