using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Core.Scripting;
using Core.Scripting.DM;
using Core.Scripting.LuaSystem;
using Core.VM.Runtime;

namespace Core
{
    public class ScriptManager
    {
        private readonly IEnumerable<IScriptSystem> _systems;
        private readonly string _scriptsRoot;

        public ScriptManager(IEnumerable<IScriptSystem> systems, Project project)
        {
            _systems = systems;
            _scriptsRoot = project.GetFullPath(Constants.ScriptsRoot);
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

        public void InvokeGlobalEvent(string eventName)
        {
            foreach (var sys in _systems)
            {
                sys.InvokeEvent(eventName);
            }
        }

        public void ExecuteCommand(string command)
        {
            var luaSystem = _systems.OfType<LuaSystem>().FirstOrDefault();
            luaSystem?.ExecuteString(command);
        }

        public DreamThread? CreateThread(string procName)
        {
            var dmSystem = _systems.OfType<DmSystem>().FirstOrDefault();
            return dmSystem?.CreateThread(procName);
        }
    }
}
