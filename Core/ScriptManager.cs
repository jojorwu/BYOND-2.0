using Shared;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Core.Scripting.DM;
using Core.Scripting.LuaSystem;
using Core.VM.Runtime;
using System.Linq;

namespace Core
{
    public class ScriptManager : IScriptManager
    {
        private readonly IEnumerable<IScriptSystem> _systems;
        private readonly string _scriptsRoot;

        public ScriptManager(IProject project, IEnumerable<IScriptSystem> systems)
        {
            _scriptsRoot = project.GetFullPath(Constants.ScriptsRoot);
            _systems = systems;
        }

        public async Task Initialize()
        {
            if (!Directory.Exists(_scriptsRoot))
            {
                Directory.CreateDirectory(_scriptsRoot);
            }

            foreach (var sys in _systems)
            {
                sys.Initialize();
                await sys.LoadScripts(_scriptsRoot);
            }
        }

        public async Task ReloadAll()
        {
            foreach (var sys in _systems)
            {
                sys.Reload();
                await sys.LoadScripts(_scriptsRoot);
            }
        }

        public IEnumerable<IScriptThread> InvokeGlobalEvent(string eventName)
        {
            var threads = new List<IScriptThread>();
            foreach (var sys in _systems)
            {
                var thread = sys.InvokeEvent(eventName);
                if (thread != null)
                {
                    threads.Add(thread);
                }
            }
            return threads;
        }

        public string? ExecuteCommand(string command)
        {
            foreach (var system in _systems)
            {
                var result = system.ExecuteString(command);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }

        public IScriptThread? CreateThread(string procName, IGameObject? associatedObject = null)
        {
            foreach (var system in _systems.OfType<IThreadSupportingScriptSystem>())
            {
                var thread = system.CreateThread(procName, associatedObject);
                if (thread != null)
                {
                    return thread;
                }
            }
            return null;
        }

    }
}
