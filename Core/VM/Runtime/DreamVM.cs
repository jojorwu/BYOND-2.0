using System.Collections.Generic;
using System.Linq;
using Core.VM.Procs;

namespace Core.VM.Runtime
{
    public class DreamVM
    {
        public List<string> Strings { get; } = new();
        public Dictionary<string, DreamProc> Procs { get; } = new();
        public ObjectTypeManager ObjectTypeManager { get; }
        private readonly ServerSettings _settings;

        public DreamVM(ServerSettings settings)
        {
            _settings = settings;
            ObjectTypeManager = new ObjectTypeManager();
        }

        public DreamThread? CreateWorldNewThread()
        {
            if (Procs.TryGetValue("/world/proc/New", out var worldNewProc))
            {
                return new DreamThread(worldNewProc, this, _settings.VmMaxInstructions);
            }
            return null;
        }
    }
}
