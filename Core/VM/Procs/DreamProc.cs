namespace Core.VM.Procs
{
    public class DreamProc
    {
        public byte[] Bytecode { get; }
        public string Name { get; }
        public int ArgumentCount { get; }
        public int LocalVariableCount { get; }

        public DreamProc(string name, byte[] bytecode, int argumentCount, int localVariableCount)
        {
            Name = name;
            Bytecode = bytecode;
            ArgumentCount = argumentCount;
            LocalVariableCount = localVariableCount;
        }
    }
}
