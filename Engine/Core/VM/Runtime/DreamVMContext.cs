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
        private SpinLock _globalsLock = new(false);
        private SpinLock _procsLock = new(false);
        private SpinLock _stringsLock = new(false);
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
            bool lockTaken = false;
            try
            {
                _globalsLock.Enter(ref lockTaken);
                return (index < Globals.Count) ? Globals[index] : DreamValue.Null;
            }
            finally
            {
                if (lockTaken) _globalsLock.Exit(false);
            }
        }

        public void SetGlobal(int index, DreamValue value)
        {
            if (index < 0 || index >= MaxGlobals) return;
            bool lockTaken = false;
            try
            {
                _globalsLock.Enter(ref lockTaken);
                while (Globals.Count <= index) Globals.Add(DreamValue.Null);
                Globals[index] = value;
            }
            finally
            {
                if (lockTaken) _globalsLock.Exit(false);
            }
        }

        public void Reset()
        {
            bool gLockTaken = false, pLockTaken = false, sLockTaken = false;
            try
            {
                _globalsLock.Enter(ref gLockTaken);
                _procsLock.Enter(ref pLockTaken);
                _stringsLock.Enter(ref sLockTaken);

                Strings.Clear();
                Procs.Clear();
                AllProcs.Clear();
                Globals.Clear();
                GlobalNames.Clear();
                ListType = null;
            }
            finally
            {
                if (sLockTaken) _stringsLock.Exit(false);
                if (pLockTaken) _procsLock.Exit(false);
                if (gLockTaken) _globalsLock.Exit(false);
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
