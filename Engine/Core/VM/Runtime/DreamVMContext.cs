using System.Collections.Generic;
using System.Threading;
using Shared;

namespace Core.VM.Runtime
{
    public class DreamVMContext
    {
        private const int MaxGlobals = 1000000;
        private readonly ReaderWriterLockSlim _globalLock = new();
        public List<string> Strings { get; } = new();
        public Dictionary<string, IDreamProc> Procs { get; } = new();
        public List<IDreamProc> AllProcs { get; } = new();
        public List<DreamValue> Globals { get; } = new();
        public Dictionary<string, int> GlobalNames { get; } = new();
        public ObjectType? ListType { get; set; }
        public DreamObject? World { get; set; }
        public IObjectTypeManager? ObjectTypeManager { get; set; }
        public IGameState? GameState { get; set; }
        public IGameApi? GameApi { get; set; }
        public IScriptHost? ScriptHost { get; set; }

        public DreamValue GetGlobal(int index)
        {
            if (index < 0) return DreamValue.Null;
            _globalLock.EnterReadLock();
            try
            {
                return (index < Globals.Count) ? Globals[index] : DreamValue.Null;
            }
            finally
            {
                _globalLock.ExitReadLock();
            }
        }

        public void SetGlobal(int index, DreamValue value)
        {
            if (index < 0 || index >= MaxGlobals) return;
            _globalLock.EnterWriteLock();
            try
            {
                while (Globals.Count <= index) Globals.Add(DreamValue.Null);
                Globals[index] = value;
            }
            finally
            {
                _globalLock.ExitWriteLock();
            }
        }

        public void Reset()
        {
            _globalLock.EnterWriteLock();
            try
            {
                Strings.Clear();
                Procs.Clear();
                AllProcs.Clear();
                Globals.Clear();
                GlobalNames.Clear();
                ListType = null;
            }
            finally
            {
                _globalLock.ExitWriteLock();
            }
        }
    }
}
