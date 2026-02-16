using Shared;
using System.Collections.Generic;
using System.Linq;
using Core.VM.Procs;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.VM.Runtime
{
    public class DreamVM : IDreamVM, IDisposable
    {
        public DreamVMContext Context { get; } = new();
        public List<string> Strings => Context.Strings;
        public Dictionary<string, IDreamProc> Procs => Context.Procs;
        public List<DreamValue> Globals => Context.Globals;
        public ObjectType? ListType { get => Context.ListType; set => Context.ListType = value; }
        public IObjectTypeManager? ObjectTypeManager { get => Context.ObjectTypeManager; set => Context.ObjectTypeManager = value; }
        public IGameState? GameState { get => Context.GameState; set => Context.GameState = value; }
        public IGameApi? GameApi { get => Context.GameApi; set => Context.GameApi = value; }

        private readonly ServerSettings _settings;
        private readonly ILogger<DreamVM> _logger;
        private readonly IEnumerable<INativeProcProvider> _nativeProcProviders;
        private readonly IBytecodeInterpreter _interpreter;

        public DreamVM(IOptions<ServerSettings> settings, ILogger<DreamVM> logger, IEnumerable<INativeProcProvider> nativeProcProviders, IBytecodeInterpreter? interpreter = null)
        {
            _settings = settings.Value;
            _logger = logger;
            _nativeProcProviders = nativeProcProviders;
            _interpreter = interpreter ?? new BytecodeInterpreter();
        }

        public void Initialize()
        {
            _logger.LogInformation("Initializing Dream VM...");
            RegisterNativeProcs();
        }

        private void RegisterNativeProcs()
        {
            foreach (var provider in _nativeProcProviders)
            {
                var procs = provider.GetNativeProcs();
                foreach (var kvp in procs)
                {
                    _logger.LogDebug("Registering native proc: {ProcName}", kvp.Key);
                    Context.Procs[kvp.Key] = kvp.Value;
                }
            }
        }

        public void Reset()
        {
            _logger.LogInformation("Resetting Dream VM state...");
            Context.Reset();
        }

        public DreamThread? CreateWorldNewThread()
        {
            if (Procs.TryGetValue("/world/proc/New", out var worldNewProc) && worldNewProc is DreamProc dreamProc)
            {
                return new DreamThread(dreamProc, Context, _settings.VmMaxInstructions, interpreter: _interpreter);
            }
            _logger.LogError("/world/proc/New not found. Is the script compiled correctly?");
            return null;
        }

        public IScriptThread? CreateThread(string procName, IGameObject? associatedObject = null)
        {
            if (Procs.TryGetValue(procName, out var proc) && proc is DreamProc dreamProc)
            {
                return new DreamThread(dreamProc, Context, _settings.VmMaxInstructions, associatedObject, _interpreter);
            }

            _logger.LogWarning("Could not find proc '{ProcName}' to create a thread.", procName);
            return null;
        }

        public void Dispose()
        {
            Context.Dispose();
        }
    }
}
