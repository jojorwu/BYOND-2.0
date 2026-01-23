using Shared;
using System.Collections.Generic;
using System.Linq;
using Core.VM.Procs;

namespace Core.VM.Runtime
{
    public class DreamVM : IDreamVM
    {
        public List<string> Strings { get; } = new();
        public List<IDreamProc> ProcsById { get; } = new();
        public Dictionary<string, int> ProcNameIds { get; } = new();
        private readonly ServerSettings _settings;

        IReadOnlyList<IDreamProc> IDreamVM.ProcsById => ProcsById;
        IReadOnlyDictionary<string, int> IDreamVM.ProcNameIds => ProcNameIds;

        public DreamVM(ServerSettings settings)
        {
            _settings = settings;
        }

        public DreamThread? CreateWorldNewThread()
        {
            if (ProcNameIds.TryGetValue("/world/proc/New", out var procId))
            {
                var worldNewProc = ProcsById[procId];
                if (worldNewProc is DreamProc dreamProc)
                {
                    return new DreamThread(dreamProc, this, _settings.VmMaxInstructions);
                }
            }
            Console.WriteLine("Error: /world/proc/New not found. Is the script compiled correctly?");
            return null;
        }

        public IScriptThread? CreateThread(string procName, IGameObject? associatedObject = null)
        {
            if (ProcNameIds.TryGetValue(procName, out var procId))
            {
                var proc = ProcsById[procId];
                if (proc is DreamProc dreamProc)
                {
                    return new DreamThread(dreamProc, this, _settings.VmMaxInstructions, associatedObject);
                }
            }

            Console.WriteLine($"Warning: Could not find proc '{procName}' to create a thread.");
            return null;
        }
    }
}
