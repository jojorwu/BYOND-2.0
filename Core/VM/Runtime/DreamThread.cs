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
        public DreamThreadState State { get; private set; } = DreamThreadState.Running;

        private readonly DreamVM _vm;
        private readonly int _maxInstructions;
        private int _totalInstructionsExecuted;

        public DreamThread(DreamProc proc, DreamVM vm, int maxInstructions)
        {
            CurrentProc = proc;
            _vm = vm;
            _maxInstructions = maxInstructions;
        }

        public void Push(DreamValue value)
        {
            Stack.Push(value);
        }

        public DreamValue Pop()
        {
            return Stack.Pop();
        }

        private byte ReadByte()
        {
            if (PC + 1 > CurrentProc.Bytecode.Length)
                throw new Exception("Attempted to read past the end of the bytecode.");
            return CurrentProc.Bytecode[PC++];
        }

        private int ReadInt32()
        {
            if (PC + 4 > CurrentProc.Bytecode.Length)
                throw new Exception("Attempted to read past the end of the bytecode.");
            var value = BitConverter.ToInt32(CurrentProc.Bytecode, PC);
            PC += 4;
            return value;
        }

        private float ReadSingle()
        {
            if (PC + 4 > CurrentProc.Bytecode.Length)
                throw new Exception("Attempted to read past the end of the bytecode.");
            var value = BitConverter.ToSingle(CurrentProc.Bytecode, PC);
            PC += 4;
            return value;
        }

        public DreamThreadState Run(int instructionBudget)
        {
            if (State != DreamThreadState.Running)
                return State;

            var instructionsExecutedThisTick = 0;
            while (PC < CurrentProc.Bytecode.Length)
            {
                if (instructionsExecutedThisTick++ >= instructionBudget)
                    return DreamThreadState.Running; // Budget exhausted, will resume next tick

                if (_totalInstructionsExecuted++ > _maxInstructions)
                {
                    State = DreamThreadState.Error;
                    Console.WriteLine("Error: Instruction limit exceeded.");
                    return State;
                }

                var opcode = (Opcode)ReadByte();
                switch (opcode)
                {
                    case Opcode.PushString:
                    {
                        var stringId = ReadInt32();
                        Push(new DreamValue(_vm.Strings[stringId]));
                        break;
                    }
                    case Opcode.PushFloat:
                    {
                        var value = ReadSingle();
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
                    case Opcode.Subtract:
                    {
                        var b = Pop();
                        var a = Pop();
                        Push(a - b);
                        break;
                    }
                    case Opcode.Multiply:
                    {
                        var b = Pop();
                        var a = Pop();
                        Push(a * b);
                        break;
                    }
                    case Opcode.Divide:
                    {
                        var b = Pop();
                        var a = Pop();
                        Push(a / b);
                        break;
                    }
                    case Opcode.CompareEquals:
                    {
                        var b = Pop();
                        var a = Pop();
                        Push(new DreamValue(a == b ? 1 : 0));
                        break;
                    }
                    case Opcode.CompareNotEquals:
                    {
                        var b = Pop();
                        var a = Pop();
                        Push(new DreamValue(a != b ? 1 : 0));
                        break;
                    }
                    case Opcode.Output:
                    {
                        var value = Pop();
                        Console.WriteLine(value.ToString());
                        break;
                    }
                    case Opcode.Return:
                    {
                        State = DreamThreadState.Finished;
                        return State;
                    }

                    default:
                        State = DreamThreadState.Error;
                        throw new Exception($"Unknown opcode: {opcode}");
                }
            }

            State = DreamThreadState.Finished;
            return State;
        }
    }
}
