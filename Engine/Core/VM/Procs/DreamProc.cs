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
        public byte[] Bytecode { get; }
        public string Name { get; }
        public string[] Arguments { get; }
        public int LocalVariableCount { get; }

        public IDreamProc? ParentProc { get; set; }

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
