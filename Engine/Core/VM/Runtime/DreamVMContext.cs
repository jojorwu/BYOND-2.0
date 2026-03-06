using System.Collections.Generic;
using System.Threading;
using Shared;
using Shared.Interfaces;
using System;

namespace Core.VM.Runtime
{
    public class DreamVMContext : IDisposable
    {
        private const int MaxGlobals = 100000000;
        private readonly ReaderWriterLockSlim _contextLock = new(LockRecursionPolicy.SupportsRecursion);
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
        public IObjectFactory? ObjectFactory { get; set; }

        public DreamValue GetGlobal(int index)
        {
            if (index < 0) return DreamValue.Null;
            _contextLock.EnterReadLock();
            try
            {
                return (index < Globals.Count) ? Globals[index] : DreamValue.Null;
            }
            finally
            {
                _contextLock.ExitReadLock();
            }
        }

        public void SetGlobal(int index, DreamValue value)
        {
            if (index < 0 || index >= MaxGlobals) return;
            _contextLock.EnterWriteLock();
            try
            {
                while (Globals.Count <= index) Globals.Add(DreamValue.Null);
                Globals[index] = value;
            }
            finally
            {
                _contextLock.ExitWriteLock();
            }
        }

        public void Reset()
        {
            _contextLock.EnterWriteLock();
            try
            {
                Strings.Clear();
                Procs.Clear();
                AllProcs.Clear();
                Globals.Clear();
                GlobalNames.Clear();
                ListType = null;
                World = null;
            }
            finally
            {
                _contextLock.ExitWriteLock();
            }
        }

        public void Dispose()
        {
            _contextLock.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
