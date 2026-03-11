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

        public DreamProc(string name, byte[] bytecode, string[] arguments, int localVariableCount, System.Collections.Generic.IReadOnlyList<string>? strings = null)
        {
            Name = name;
            Bytecode = Utils.BytecodeOptimizer.Optimize(bytecode, strings);
            Arguments = arguments;
            LocalVariableCount = localVariableCount;
            _inlineCache = new InlineCacheEntry[Bytecode.Length];
        }
    }
}
