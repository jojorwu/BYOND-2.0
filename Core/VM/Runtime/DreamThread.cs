using System;
using System.Collections.Generic;
using Core.VM.Procs;
using Core.VM.Types;
using Shared;

namespace Core.VM.Runtime
{
    public partial class DreamThread : IScriptThread
    {
        public List<DreamValue> Stack { get; } = new(128);
        public Stack<CallFrame> CallStack { get; } = new();

        public DreamProc CurrentProc => CallStack.Peek().Proc;
        public DreamThreadState State { get; private set; } = DreamThreadState.Running;
        public IGameObject? AssociatedObject { get; }

        private readonly DreamVM _vm;
        private readonly int _maxInstructions;
        private int _totalInstructionsExecuted;

        public DreamThread(DreamProc proc, DreamVM vm, int maxInstructions, IGameObject? associatedObject = null)
        {
            _vm = vm;
            _maxInstructions = maxInstructions;
            AssociatedObject = associatedObject;

            CallStack.Push(new CallFrame(proc, 0, 0, null));
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

        private byte ReadByte(DreamProc proc, ref int pc)
        {
            if (pc + 1 > proc.Bytecode.Length)
                throw new Exception("Attempted to read past the end of the bytecode.");
            return proc.Bytecode[pc++];
        }

        private int ReadInt32(DreamProc proc, ref int pc)
        {
            if (pc + 4 > proc.Bytecode.Length)
                throw new Exception("Attempted to read past the end of the bytecode.");
            var value = BitConverter.ToInt32(proc.Bytecode, pc);
            pc += 4;
            return value;
        }

        private float ReadSingle(DreamProc proc, ref int pc)
        {
            if (pc + 4 > proc.Bytecode.Length)
                throw new Exception("Attempted to read past the end of the bytecode.");
            var value = BitConverter.ToSingle(proc.Bytecode, pc);
            pc += 4;
            return value;
        }

        private void SavePC(int pc)
        {
            if (CallStack.Count > 0)
            {
                var frame = CallStack.Pop();
                frame.PC = pc;
                CallStack.Push(frame);
            }
        }

        public DreamThreadState Run(int instructionBudget)
        {
            if (State != DreamThreadState.Running)
                return State;

            var instructionsExecutedThisTick = 0;

            var frame = CallStack.Peek();
            var proc = frame.Proc;
            var pc = frame.PC;

            while (State == DreamThreadState.Running)
            {
                if (instructionsExecutedThisTick++ >= instructionBudget)
                {
                    SavePC(pc);
                    return DreamThreadState.Running; // Budget exhausted, will resume next tick
                }

                if (_totalInstructionsExecuted++ > _maxInstructions)
                {
                    State = DreamThreadState.Error;
                    Console.WriteLine("Error: Instruction limit exceeded.");
                    SavePC(pc);
                    return State;
                }

                // If we've executed past the end of the bytecode, it's an implicit return.
                if (pc >= proc.Bytecode.Length)
                {
                    Push(DreamValue.Null);
                    Opcode_Return(ref proc, ref pc);
                    if(State == DreamThreadState.Running)
                        frame = CallStack.Peek();
                    continue;
                }

                try
                {
                    var opcode = (Opcode)ReadByte(proc, ref pc);
                    switch (opcode)
                    {
                        case Opcode.PushString: Opcode_PushString(proc, ref pc); break;
                        case Opcode.PushFloat: Opcode_PushFloat(proc, ref pc); break;
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
                        case Opcode.Call:
                            Opcode_Call(proc, ref pc);
                            frame = CallStack.Peek();
                            proc = frame.Proc;
                            pc = frame.PC;
                            break;
                        case Opcode.Jump: Opcode_Jump(proc, ref pc); break;
                        case Opcode.JumpIfFalse: Opcode_JumpIfFalse(proc, ref pc); break;
                        case Opcode.Output: Opcode_Output(); break;
                        case Opcode.Return:
                            Opcode_Return(ref proc, ref pc);
                            if(State == DreamThreadState.Running)
                                frame = CallStack.Peek();
                            break;
                        case Opcode.BitAnd: Opcode_BitAnd(); break;
                        case Opcode.BitOr: Opcode_BitOr(); break;
                        case Opcode.BitXor: Opcode_BitXor(); break;
                        case Opcode.BitNot: Opcode_BitNot(); break;
                        case Opcode.BitShiftLeft: Opcode_BitShiftLeft(); break;
                        case Opcode.BitShiftRight: Opcode_BitShiftRight(); break;
                        case Opcode.GetVariable: Opcode_GetVariable(proc, frame, ref pc); break;
                        case Opcode.SetVariable: Opcode_SetVariable(proc, frame, ref pc); break;
                        case Opcode.AssignNoPush: Opcode_AssignNoPush(proc, frame, ref pc); break;
                        default:
                            State = DreamThreadState.Error;
                            throw new Exception($"Unknown opcode: {opcode}");
                    }
                }
                catch (Exception e)
                {
                    State = DreamThreadState.Error;
                    Console.WriteLine($"Error during script execution: {e.Message}");
                    SavePC(pc);
                    return State;
                }

                if (State != DreamThreadState.Running) // Check state after handler execution
                {
                    SavePC(pc);
                    return State;
                }
            }
            return State;
        }
    }
}
