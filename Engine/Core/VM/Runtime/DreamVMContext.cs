using System.Collections.Generic;
using Shared;

namespace Core.VM.Runtime
{
    public class DreamVMContext
    {
        private readonly object _globalLock = new();
        public List<string> Strings { get; } = new();
        public Dictionary<string, IDreamProc> Procs { get; } = new();
        public List<IDreamProc> AllProcs { get; } = new();
        public List<DreamValue> Globals { get; } = new();
        public Dictionary<string, int> GlobalNames { get; } = new();
        public ObjectType? ListType { get; set; }
        public IObjectTypeManager? ObjectTypeManager { get; set; }
        public IGameState? GameState { get; set; }
        public IScriptHost? ScriptHost { get; set; }

        public DreamValue GetGlobal(int index)
        {
            lock (_globalLock)
            {
                return (index >= 0 && index < Globals.Count) ? Globals[index] : DreamValue.Null;
            }
        }

        public void SetGlobal(int index, DreamValue value)
        {
            lock (_globalLock)
            {
                while (Globals.Count <= index) Globals.Add(DreamValue.Null);
                Globals[index] = value;
            }
        }

        public void Reset()
        {
            lock (_globalLock)
            {
                Strings.Clear();
                Procs.Clear();
                AllProcs.Clear();
                Globals.Clear();
                GlobalNames.Clear();
                ListType = null;
            }
        }
    }
}
