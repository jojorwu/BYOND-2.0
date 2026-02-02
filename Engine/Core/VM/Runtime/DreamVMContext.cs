using System.Collections.Generic;
using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;

namespace Core.VM.Runtime
{
    public class DreamVMContext
    {
        private readonly System.Threading.ReaderWriterLockSlim _globalLock = new();
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
            _globalLock.EnterReadLock();
            try
            {
                return index >= 0 && index < Globals.Count ? Globals[index] : DreamValue.Null;
            }
            finally
            {
                _globalLock.ExitReadLock();
            }
        }

        public void SetGlobal(int index, DreamValue value)
        {
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
    }
}
