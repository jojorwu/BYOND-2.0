using Shared;
using System.Collections.Generic;
using System.Linq;
using Core.VM.Procs;

namespace Core.VM.Runtime
{
    public class DreamVM : IDreamVM
    {
        public DreamVMContext Context { get; } = new();
        public List<string> Strings => Context.Strings;
        public Dictionary<string, IDreamProc> Procs => Context.Procs;
        private readonly ServerSettings _settings;

        public DreamVM(ServerSettings settings)
        {
            _settings = settings;
        }

        public DreamThread? CreateWorldNewThread()
        {
            if (Procs.TryGetValue("/world/proc/New", out var worldNewProc) && worldNewProc is DreamProc dreamProc)
            {
                return new DreamThread(dreamProc, Context, _settings.VmMaxInstructions);
            }
            Console.WriteLine("Error: /world/proc/New not found. Is the script compiled correctly?");
            return null;
        }

        public IScriptThread? CreateThread(string procName, IGameObject? associatedObject = null)
        {
            if (Procs.TryGetValue(procName, out var proc) && proc is DreamProc dreamProc)
            {
                return new DreamThread(dreamProc, Context, _settings.VmMaxInstructions, associatedObject);
            }

            Console.WriteLine($"Warning: Could not find proc '{procName}' to create a thread.");
            return null;
        }
    }
}
