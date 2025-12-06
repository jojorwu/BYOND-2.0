using Shared;
using System.Collections.Generic;
using System.Linq;
using Core.VM.Procs;

namespace Core.VM.Runtime
{
    public class DreamVM
    {
        public List<string> Strings { get; } = new();
        public Dictionary<string, DreamProc> Procs { get; } = new();
        private readonly ServerSettings _settings;

        public DreamVM(ServerSettings settings)
        {
            _settings = settings;
        }

        public DreamThread? CreateWorldNewThread()
        {
            if (Procs.TryGetValue("/world/proc/New", out var worldNewProc))
            {
                return new DreamThread(worldNewProc, this, _settings.VmMaxInstructions);
            }
            Console.WriteLine("Error: /world/proc/New not found. Is the script compiled correctly?");
            return null;
        }
    }
}
