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
        private const int ShardCount = 64;
        private readonly System.Threading.Lock _contextLock = new();
        private readonly System.Threading.Lock[] _globalShards;

        public List<string> Strings { get; } = new();
        public System.Collections.Concurrent.ConcurrentDictionary<string, IDreamProc> Procs { get; } = new();
        public List<IDreamProc> AllProcs { get; } = new();
        private volatile DreamValue[] _globals = Array.Empty<DreamValue>();
        public IList<DreamValue> Globals => _globals;
        public System.Collections.Concurrent.ConcurrentDictionary<string, int> GlobalNames { get; } = new();

        public DreamVMContext()
        {
            _globalShards = new System.Threading.Lock[ShardCount];
            for (int i = 0; i < ShardCount; i++) _globalShards[i] = new();
        }

        public void InitializeGlobals(int count)
        {
            using (_contextLock.EnterScope())
            {
                if (count > MaxGlobals) throw new ArgumentOutOfRangeException(nameof(count));
                var newGlobals = new DreamValue[count];
                Array.Fill(newGlobals, DreamValue.Null);
                _globals = newGlobals;
            }
        }
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
            if ((uint)index >= (uint)globals.Length) return DreamValue.Null;

            using (_globalShards[index % ShardCount].EnterScope())
            {
                // Re-read current globals array after locking to handle potential resize
                globals = _globals;
                if ((uint)index < (uint)globals.Length) return globals[index];
                return DreamValue.Null;
            }
        }

        public void SetGlobal(int index, DreamValue value)
        {
            if (index < 0 || index >= MaxGlobals) return;

            // First ensure capacity with a coarse lock
            if ((uint)index >= (uint)_globals.Length)
            {
                using (_contextLock.EnterScope())
                {
                    if (index >= _globals.Length)
                    {
                        int newSize = Math.Max(index + 1, _globals.Length * 2);
                        if (newSize < _globals.Length * 2) newSize = _globals.Length * 2;

                        var newGlobals = new DreamValue[newSize];
                        _globals.CopyTo(newGlobals, 0);
                        for (int j = _globals.Length; j < newSize; j++) newGlobals[j] = DreamValue.Null;
                        _globals = newGlobals;
                    }
                }
            }

            // Then set with a fine-grained shard lock
            using (_globalShards[index % ShardCount].EnterScope())
            {
                _globals[index] = value;
            }
        }

        public void Reset()
        {
            using (_contextLock.EnterScope())
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
            GC.SuppressFinalize(this);
        }
    }
}
