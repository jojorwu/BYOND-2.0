namespace Core.VM.Procs
{
    public class DreamProc(string name, byte[] bytecode, int argumentCount, int localVariableCount)
    {
        public byte[] Bytecode { get; } = bytecode;
        public string Name { get; } = name;
        public int ArgumentCount { get; } = argumentCount;
        public int LocalVariableCount { get; } = localVariableCount;
    }
}
