using System.Collections.Generic;
using System.Linq;
using Core.VM.Procs;

namespace Core.VM.Runtime
{
    public class DreamVM
    {
        public List<string> Strings { get; } = new();
        public List<DreamProc> Procs { get; } = new();

        public void RunWorldNew()
        {
            var worldNewProc = Procs.FirstOrDefault(p => p.Name == "/world/proc/New");
            if (worldNewProc != null)
            {
                var thread = new DreamThread(worldNewProc, this);
                thread.Run();
            }
        }
    }
}
