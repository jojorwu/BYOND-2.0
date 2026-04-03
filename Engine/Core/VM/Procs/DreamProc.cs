using Shared;

using System.Runtime.InteropServices;

namespace Core.VM.Procs
{
    [StructLayout(LayoutKind.Auto)]
    internal struct InlineCacheEntry
    {
        public ObjectType? ObjectType;
        public int VariableIndex;
        public IDreamProc? CachedProc;
    }

    public class DreamProc : IDreamProc
    {
        private static readonly IDreamProc NoParentSentinel = new NativeProc("__no_parent__", (t, i, a) => DreamValue.Null);

        public byte[] Bytecode { get; }
        public string Name { get; }
        public string[] Arguments { get; }
        public int LocalVariableCount { get; }

        private IDreamProc? _parentProc;
        private bool _parentProcSearched;

        public IDreamProc? ParentProc
        {
            get => _parentProc == NoParentSentinel ? null : _parentProc;
            set
            {
                _parentProc = value ?? NoParentSentinel;
                _parentProcSearched = true;
            }
        }

        public bool ParentProcSearched => _parentProcSearched;

        internal InlineCacheEntry[] _inlineCache;

        public DreamProc(string name, byte[] bytecode, string[] arguments, int localVariableCount, System.Collections.Generic.IReadOnlyList<string>? strings = null, int totalProcs = 0, int totalTypes = 0)
        {
            Name = name;
            Bytecode = Utils.BytecodeOptimizer.Optimize(bytecode, strings);
            Utils.BytecodeVerifier.Verify(Bytecode, strings, totalProcs, totalTypes);
            Arguments = arguments;
            LocalVariableCount = localVariableCount;
            // The cache size must be at least as large as the bytecode to allow per-instruction caching safely.
            _inlineCache = new InlineCacheEntry[Bytecode.Length + 1];
        }
    }
}
