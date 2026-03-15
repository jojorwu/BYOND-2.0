using Shared;
using Shared.Interfaces;
using Shared.Services;
using System.Collections.Generic;
using System.Linq;
using Core.VM.Procs;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.VM.Runtime
{
    public class DreamVM : EngineService, IDreamVM, IDisposable
    {
        public DreamVMContext Context { get; } = new();
        public List<string> Strings => Context.Strings;
        public Dictionary<string, IDreamProc> Procs => Context.Procs;
        public List<IDreamProc> AllProcs => Context.AllProcs;
        public IList<DreamValue> Globals => Context.Globals;
        public Dictionary<string, int> GlobalNames => Context.GlobalNames;
        public ObjectType? ListType { get => Context.ListType; set => Context.ListType = value; }
        public DreamObject? World { get => Context.World; set => Context.World = value; }
        public IObjectTypeManager? ObjectTypeManager { get => Context.ObjectTypeManager; set => Context.ObjectTypeManager = value; }
        public IGameState? GameState { get => Context.GameState; set => Context.GameState = value; }
        public IGameApi? GameApi { get => Context.GameApi; set => Context.GameApi = value; }

        private readonly ILogger<DreamVM> _logger;
        private readonly IEnumerable<INativeProcProvider> _nativeProcProviders;
        private readonly IBytecodeInterpreter _interpreter;
        private readonly IObjectFactory? _objectFactory;

        private readonly int _maxInstructions;

        public DreamVM(IOptions<DreamVmConfiguration> config, ILogger<DreamVM> logger, IEnumerable<INativeProcProvider> nativeProcProviders, IObjectFactory? objectFactory = null, IBytecodeInterpreter? interpreter = null)
        {
            _maxInstructions = config.Value.MaxInstructions;
            _logger = logger;
            _nativeProcProviders = nativeProcProviders;
            _objectFactory = objectFactory;
            _interpreter = interpreter ?? new BytecodeInterpreter();
            Context.ObjectFactory = objectFactory;
        }

        public void Initialize()
        {
            _logger.LogInformation("Initializing Dream VM...");
            RegisterNativeProcs();
        }

        public override Task InitializeAsync()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Initialize();
            sw.Stop();
            InitializationDurationMs = sw.ElapsedMilliseconds;
            return Task.CompletedTask;
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
                return new DreamThread(dreamProc, Context, _maxInstructions, interpreter: _interpreter);
            }
            _logger.LogError("/world/proc/New not found. Is the script compiled correctly?");
            return null;
        }

        public IScriptThread? CreateThread(string procName, IGameObject? associatedObject = null)
        {
            if (Procs.TryGetValue(procName, out var proc) && proc is DreamProc dreamProc)
            {
                return new DreamThread(dreamProc, Context, _maxInstructions, associatedObject, _interpreter);
            }

            _logger.LogWarning("Could not find proc '{ProcName}' to create a thread.", procName);
            return null;
        }

        public void Dispose()
        {
            Context.Dispose();
        }

        public override IEnumerable<Type> Dependencies => new[] { typeof(IObjectTypeManager) };

        public override Dictionary<string, object> GetDiagnosticInfo()
        {
            var info = base.GetDiagnosticInfo();
            info["ProcCount"] = AllProcs.Count;
            info["GlobalCount"] = Globals.Count;
            info["StringCount"] = Strings.Count;
            info["MaxInstructions"] = _maxInstructions;
            return info;
        }
    }
}
