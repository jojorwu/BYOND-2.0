using Shared;

namespace Core.VM.Procs
{
    public class DreamProc : IDreamProc
    {
        public byte[] Bytecode { get; }
        public string Name { get; }
        public string[] Arguments { get; }
        public int LocalVariableCount { get; }

        public DreamProc(string name, byte[] bytecode, string[] arguments, int localVariableCount, System.Collections.Generic.IReadOnlyList<string>? strings = null)
        {
            Name = name;
            Bytecode = Utils.BytecodeOptimizer.Optimize(bytecode, strings);
            Arguments = arguments;
            LocalVariableCount = localVariableCount;
        }
    }
}
