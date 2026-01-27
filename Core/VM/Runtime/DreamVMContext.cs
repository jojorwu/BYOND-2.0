using System.Collections.Generic;
using Shared;

namespace Core.VM.Runtime
{
    public class DreamVMContext
    {
        public List<string> Strings { get; } = new();
        public Dictionary<string, IDreamProc> Procs { get; } = new();
        public List<DreamValue> Globals { get; } = new();
        public ObjectType? ListType { get; set; }
    }
}
