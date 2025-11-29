using System;
using System.Collections.Generic;
using Core.VM.Opcodes;
using Core.VM.Procs;
using Core.VM.Types;

namespace Core.VM.Runtime
{
    public class DreamThread
    {
        public Stack<DreamValue> Stack { get; } = new();
        public int PC { get; set; } // Program Counter
        public DreamProc CurrentProc { get; }
        private readonly DreamVM _vm;

        public DreamThread(DreamProc proc, DreamVM vm)
        {
            CurrentProc = proc;
            _vm = vm;
        }

        public void Push(DreamValue value)
        {
            Stack.Push(value);
        }

        public DreamValue Pop()
        {
            return Stack.Pop();
        }

        public void Run()
        {
            while (PC < CurrentProc.Bytecode.Length)
            {
                var opcode = (Opcode)CurrentProc.Bytecode[PC++];
                switch (opcode)
                {
                    case Opcode.PushString:
                    {
                        if (PC + 4 > CurrentProc.Bytecode.Length)
                            throw new Exception("Attempted to read past the end of the bytecode.");
                        var stringId = BitConverter.ToInt32(CurrentProc.Bytecode, PC);
                        PC += 4;
                        Push(new DreamValue(_vm.Strings[stringId]));
                        break;
                    }
                    case Opcode.PushFloat:
                    {
                        if (PC + 4 > CurrentProc.Bytecode.Length)
                            throw new Exception("Attempted to read past the end of the bytecode.");
                        var value = BitConverter.ToSingle(CurrentProc.Bytecode, PC);
                        PC += 4;
                        Push(new DreamValue(value));
                        break;
                    }
                    case Opcode.Add:
                    {
                        var b = Pop();
                        var a = Pop();
                        Push(a + b);
                        break;
                    }
                    case Opcode.Output:
                    {
                        var value = Pop();
                        Console.WriteLine(value.ToString());
                        break;
                    }

                    default:
                        throw new Exception($"Unknown opcode: {opcode}");
                }
            }
        }
    }
}
