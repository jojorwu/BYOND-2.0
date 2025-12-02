using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Core.Scripting;
using Core.Scripting.CSharp;
using Core.Scripting.DM;
using Core.Scripting.LuaSystem;
using Core.VM;
using Core.VM.Runtime;

namespace Core
{
    public class ScriptManager
    {
        private readonly List<IScriptSystem> _systems = new();
        private readonly string _scriptsRoot;

        public ScriptManager(GameApi gameApi, ObjectTypeManager typeManager, Project project, DreamVM dreamVM, GameState gameState)
        {
            _scriptsRoot = project.GetFullPath(Constants.ScriptsRoot);

            _systems.Add(new CSharpSystem(gameApi));
            _systems.Add(new LuaSystem(gameApi, gameState));
            _systems.Add(new DmSystem(typeManager, project, dreamVM));
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
            foreach (var sys in _systems)
            {
                if (sys is LuaSystem luaSystem)
                {
                    luaSystem.ExecuteString(command);
                    return;
                }
            }
        }

        public DreamThread? CreateThread(string procName)
        {
            foreach (var sys in _systems)
            {
                if (sys is DmSystem dmSystem)
                {
                    return dmSystem.CreateThread(procName);
                }
            }
            return null;
        }
    }
}
