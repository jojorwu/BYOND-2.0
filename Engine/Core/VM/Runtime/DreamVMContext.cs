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
        private volatile DreamValue[] _globals = Array.Empty<DreamValue>();
        public IList<DreamValue> Globals => _globals;
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
            var globals = _globals;
            if ((uint)index < (uint)globals.Length) return globals[index];
            return DreamValue.Null;
        }

        public void SetGlobal(int index, DreamValue value)
        {
            if (index < 0 || index >= MaxGlobals) return;
            var globals = _globals;
            if ((uint)index >= (uint)globals.Length)
            {
                lock (_contextLock)
                {
                    if (index >= _globals.Length)
                    {
                        int newSize = Math.Max(index + 1, _globals.Length * 2);
                        var newGlobals = new DreamValue[newSize];
                        _globals.CopyTo(newGlobals, 0);
                        for (int i = _globals.Length; i < newSize; i++) newGlobals[i] = DreamValue.Null;
                        _globals = newGlobals;
                    }
                }
            }
            _globals[index] = value;
        }

        public void Reset()
        {
            lock (_contextLock)
            {
                Strings.Clear();
                Procs.Clear();
                AllProcs.Clear();
                _globals = Array.Empty<DreamValue>();
                GlobalNames.Clear();
                ListType = null;
                World = null;
            }
        }

        public void Dispose()
        {
            _contextLock.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
