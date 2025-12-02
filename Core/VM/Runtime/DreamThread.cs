using System;
using System.Collections.Generic;
using Core.VM.Opcodes;
using Core.VM.Procs;
using Core.VM.Types;

namespace Core.VM.Runtime
{
    public partial class DreamThread
    {
        public List<DreamValue> Stack { get; } = new(128);
        public Stack<CallFrame> CallStack { get; } = new();

        // Convenience properties to access the current frame's data
        public DreamProc CurrentProc => CallStack.Peek().Proc;
        public int PC
        {
            get => CallStack.Peek().PC;
            set => CallStack.Peek().PC = value;
        }

        public DreamThreadState State { get; private set; } = DreamThreadState.Running;

        private readonly DreamVM _vm;
        private readonly int _maxInstructions;
        private int _totalInstructionsExecuted;

        public DreamThread(DreamProc proc, DreamVM vm, int maxInstructions)
        {
            _vm = vm;
            _maxInstructions = maxInstructions;

            CallStack.Push(new CallFrame(proc, -1, 0)); // PC is initialized to 0 inside CallFrame
        }

        public void Push(DreamValue value)
        {
            Stack.Add(value);
        }

        public DreamValue Pop()
        {
            var value = Stack[^1];
            Stack.RemoveAt(Stack.Count - 1);
            return value;
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
                    case Opcode.PushString: Opcode_PushString(); break;
                    case Opcode.PushFloat: Opcode_PushFloat(); break;
                    case Opcode.Add: Opcode_Add(); break;
                    case Opcode.Subtract: Opcode_Subtract(); break;
                    case Opcode.Multiply: Opcode_Multiply(); break;
                    case Opcode.Divide: Opcode_Divide(); break;
                    case Opcode.CompareEquals: Opcode_CompareEquals(); break;
                    case Opcode.CompareNotEquals: Opcode_CompareNotEquals(); break;
                    case Opcode.CompareLessThan: Opcode_CompareLessThan(); break;
                    case Opcode.CompareGreaterThan: Opcode_CompareGreaterThan(); break;
                    case Opcode.CompareLessThanOrEqual: Opcode_CompareLessThanOrEqual(); break;
                    case Opcode.CompareGreaterThanOrEqual: Opcode_CompareGreaterThanOrEqual(); break;
                    case Opcode.Negate: Opcode_Negate(); break;
                    case Opcode.BooleanNot: Opcode_BooleanNot(); break;
                    case Opcode.PushNull: Opcode_PushNull(); break;
                    case Opcode.Pop: Opcode_Pop(); break;
                    case Opcode.Initial: Opcode_Initial(); break;
                    case Opcode.Call: Opcode_Call(); break;
                    case Opcode.Jump: Opcode_Jump(); break;
                    case Opcode.JumpIfFalse: Opcode_JumpIfFalse(); break;
                    case Opcode.Output: Opcode_Output(); break;
                    case Opcode.Return: Opcode_Return(); break;
                    case Opcode.BitAnd: Opcode_BitAnd(); break;
                    case Opcode.BitOr: Opcode_BitOr(); break;
                    case Opcode.BitXor: Opcode_BitXor(); break;
                    case Opcode.BitNot: Opcode_BitNot(); break;
                    case Opcode.BitShiftLeft: Opcode_BitShiftLeft(); break;
                    case Opcode.BitShiftRight: Opcode_BitShiftRight(); break;
                    case Opcode.PushArgument: Opcode_PushArgument(); break;
                    case Opcode.SetArgument: Opcode_SetArgument(); break;
                    case Opcode.PushLocal: Opcode_PushLocal(); break;
                    case Opcode.SetLocal: Opcode_SetLocal(); break;
                    case Opcode.CompareEquivalent: Opcode_CompareEquivalent(); break;
                    case Opcode.CompareNotEquivalent: Opcode_CompareNotEquivalent(); break;
                    default:
                        State = DreamThreadState.Error;
                        throw new Exception($"Unknown opcode: {opcode}");
                }

                if (State != DreamThreadState.Running) // Check state after handler execution
                    return State;
            }

            State = DreamThreadState.Finished;
            return State;
        }
    }
}
