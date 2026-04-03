using System.Collections.Generic;
using System.Threading;
using Shared;
using Shared.Interfaces;
using System;
using System.Runtime.CompilerServices;

namespace Core.VM.Runtime
{
    public class DreamVMContext : IDisposable
    {
        private const int MaxGlobals = 100000000;
        public int MaxObjectCount { get; set; } = 1000000;
        private const int ShardCount = 64;
        private readonly System.Threading.Lock _contextLock = new();
        private readonly System.Threading.Lock[] _globalShards;

        public List<string> Strings { get; } = new();
        public System.Collections.Concurrent.ConcurrentDictionary<string, IDreamProc> Procs { get; } = new();
        public List<IDreamProc> AllProcs { get; } = new();
        private volatile DreamValue[] _globals = Array.Empty<DreamValue>();

        /// <summary>
        /// Provides direct access to the globals array.
        /// WARNING: This should only be used for read-only access where atomicity of the 24-byte struct is not critical
        /// or in single-threaded scenarios. Use GetGlobal/SetGlobal for thread-safe access.
        /// </summary>
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DreamValue GetGlobal(int index)
        {
            var globals = _globals;
            if ((uint)index >= (uint)globals.Length) return DreamValue.Null;

            // Use shard lock to ensure atomic read of the 24-byte DreamValue struct, preventing tearing.
            using (_globalShards[index & (ShardCount - 1)].EnterScope())
            {
                // Re-read current globals array after locking to handle potential resize during thread switch
                globals = _globals;
                if ((uint)index < (uint)globals.Length) return globals[index];
                return DreamValue.Null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetGlobal(int index, DreamValue value)
        {
            if (index < 0 || index >= MaxGlobals) return;

            // First ensure capacity with a coarse lock if needed
            if ((uint)index >= (uint)_globals.Length)
            {
                EnsureCapacity(index);
            }

            // Then set with a fine-grained shard lock for atomicity
            using (_globalShards[index & (ShardCount - 1)].EnterScope())
            {
                _globals[index] = value;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void EnsureCapacity(int index)
        {
            using (_contextLock.EnterScope())
            {
                if (index >= _globals.Length)
                {
                    int newSize = Math.Max(index + 1, _globals.Length * 2);
                    if (newSize < _globals.Length * 2) newSize = _globals.Length * 2;
                    if (newSize > MaxGlobals) newSize = MaxGlobals;

                    var newGlobals = new DreamValue[newSize];
                    _globals.CopyTo(newGlobals, 0);
                    for (int j = _globals.Length; j < newSize; j++) newGlobals[j] = DreamValue.Null;
                    _globals = newGlobals;
                }
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
