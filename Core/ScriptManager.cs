using System.Collections.Generic;
using Core.Scripting;
using Core.Scripting.CSharp;
using Core.Scripting.DM;
using Core.Scripting.LuaSystem;
using Core.VM.Runtime;

namespace Core
{
    public class ScriptManager
    {
        private readonly List<IScriptSystem> _systems = new();
        private readonly string _scriptsRoot;

        public ScriptManager(GameApi gameApi, ObjectTypeManager typeManager, Project project, DreamVM dreamVM)
        {
            _scriptsRoot = project.GetFullPath(Constants.ScriptsRoot);

            // Регистрируем системы
            _systems.Add(new CSharpSystem(gameApi));
            _systems.Add(new LuaSystem(gameApi));
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

        // Пример вызова глобального события (например, старт раунда)
        public void InvokeGlobalEvent(string eventName)
        {
            foreach (var sys in _systems)
            {
                sys.InvokeEvent(eventName);
            }
        }

        public void ExecuteCommand(string command)
        {
            // Find the Lua system and execute the command
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
