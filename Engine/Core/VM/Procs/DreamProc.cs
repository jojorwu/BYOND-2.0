using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;

namespace Core.VM.Procs
{
    public class DreamProc : IDreamProc
    {
        public byte[] Bytecode { get; }
        public string Name { get; }
        public string[] Arguments { get; }
        public int LocalVariableCount { get; }

        public DreamProc(string name, byte[] bytecode, string[] arguments, int localVariableCount)
        {
            Name = name;
            Bytecode = bytecode;
            Arguments = arguments;
            LocalVariableCount = localVariableCount;
        }
    }
}
