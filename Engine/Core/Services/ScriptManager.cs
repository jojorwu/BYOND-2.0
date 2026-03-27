using Shared;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Core.Scripting.DM;
using Core.Scripting.LuaSystem;
using Core.VM.Runtime;
using System.Linq;
using Shared.Services;
using Microsoft.Extensions.Logging;

namespace Core
{
    public class ScriptManager : EngineService, IScriptManager
    {
        public override IEnumerable<System.Type> Dependencies => new[] { typeof(IDreamVM) };

        private readonly IEnumerable<IScriptSystem> _systems;
        private readonly string _scriptsRoot;
        private readonly ILogger<ScriptManager> _logger;
        private readonly Dictionary<string, long> _systemLoadTimes = new();
        private long _lastReloadDurationMs;

        public ScriptManager(IProject project, IEnumerable<IScriptSystem> systems, ILogger<ScriptManager> logger)
        {
            _scriptsRoot = project.GetFullPath(Constants.ScriptsRoot);
            _systems = systems;
            _logger = logger;
        }

        protected override async Task OnInitializeAsync()
        {
            _logger.LogInformation("Initializing Script Manager...");

            if (!Directory.Exists(_scriptsRoot))
            {
                _logger.LogInformation("Scripts directory not found. Creating at '{ScriptsRoot}'", _scriptsRoot);
                Directory.CreateDirectory(_scriptsRoot);
            }

            foreach (var sys in _systems)
            {
                var systemName = sys.GetType().Name;
                _logger.LogInformation("Initializing script system: {SystemName}", systemName);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                sys.Initialize();
                await sys.LoadScripts(_scriptsRoot);
                sw.Stop();

                _systemLoadTimes[systemName] = sw.ElapsedMilliseconds;
            }
        }

        public async Task ReloadAll()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            foreach (var sys in _systems)
            {
                sys.Reload();
                await sys.LoadScripts(_scriptsRoot);
            }
            sw.Stop();
            _lastReloadDurationMs = sw.ElapsedMilliseconds;
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
            if (command == null)
            {
                throw new System.ArgumentNullException(nameof(command));
            }

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

        public override Dictionary<string, object> GetDiagnosticInfo()
        {
            var info = base.GetDiagnosticInfo();
            info["SystemsCount"] = _systems.Count();
            info["SystemLoadTimes"] = _systemLoadTimes;
            info["LastReloadDurationMs"] = _lastReloadDurationMs;
            return info;
        }
    }
}
