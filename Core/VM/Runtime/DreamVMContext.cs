using System.Collections.Generic;
using Shared;

namespace Core.VM.Runtime
{
    public class DreamVMContext
    {
        public List<string> Strings { get; } = new();
        public Dictionary<string, IDreamProc> Procs { get; } = new();
        public List<DreamValue> Globals { get; } = new();
        public Dictionary<string, int> GlobalNames { get; } = new();
        public ObjectType? ListType { get; set; }
        public IObjectTypeManager? ObjectTypeManager { get; set; }
        public IGameState? GameState { get; set; }
    }
}
