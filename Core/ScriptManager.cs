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
        private readonly IGameState _gameState;
        private readonly string _scriptsRoot;

        public ScriptManager(IProject project, IEnumerable<IScriptSystem> systems, IGameState gameState)
        {
            _scriptsRoot = project.GetFullPath(Constants.ScriptsRoot);
            _systems = systems;
            _gameState = gameState;
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

        public IEnumerable<IGameObject> GetAllGameObjects()
        {
            return _gameState.GetAllGameObjects();
        }
    }
}
