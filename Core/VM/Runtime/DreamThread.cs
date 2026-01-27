using System;
using System.Collections.Generic;
using Core.VM.Procs;
using Core.VM.Types;
using Shared;

namespace Core.VM.Runtime
{
    public partial class DreamThread : IScriptThread
    {
        private delegate void OpcodeHandler(DreamThread thread, ref DreamProc proc, CallFrame frame, ref int pc);
        private static readonly OpcodeHandler?[] _dispatchTable = new OpcodeHandler?[256];

        static DreamThread()
        {
            _dispatchTable[(byte)Opcode.PushString] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_PushString(p, ref pc);
            _dispatchTable[(byte)Opcode.PushFloat] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_PushFloat(p, ref pc);
            _dispatchTable[(byte)Opcode.Add] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Add();
            _dispatchTable[(byte)Opcode.Subtract] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Subtract();
            _dispatchTable[(byte)Opcode.Multiply] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Multiply();
            _dispatchTable[(byte)Opcode.Divide] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Divide();
            _dispatchTable[(byte)Opcode.CompareEquals] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_CompareEquals();
            _dispatchTable[(byte)Opcode.CompareNotEquals] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_CompareNotEquals();
            _dispatchTable[(byte)Opcode.CompareLessThan] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_CompareLessThan();
            _dispatchTable[(byte)Opcode.CompareGreaterThan] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_CompareGreaterThan();
            _dispatchTable[(byte)Opcode.CompareLessThanOrEqual] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_CompareLessThanOrEqual();
            _dispatchTable[(byte)Opcode.CompareGreaterThanOrEqual] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_CompareGreaterThanOrEqual();
            _dispatchTable[(byte)Opcode.Negate] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Negate();
            _dispatchTable[(byte)Opcode.BooleanNot] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_BooleanNot();
            _dispatchTable[(byte)Opcode.PushNull] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_PushNull();
            _dispatchTable[(byte)Opcode.Pop] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Pop();
            _dispatchTable[(byte)Opcode.Call] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Call(p, ref pc);
            _dispatchTable[(byte)Opcode.Jump] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Jump(p, ref pc);
            _dispatchTable[(byte)Opcode.JumpIfFalse] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_JumpIfFalse(p, ref pc);
            _dispatchTable[(byte)Opcode.Output] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Output();
            _dispatchTable[(byte)Opcode.Return] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Return(ref p, ref pc);
            _dispatchTable[(byte)Opcode.BitAnd] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_BitAnd();
            _dispatchTable[(byte)Opcode.BitOr] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_BitOr();
            _dispatchTable[(byte)Opcode.BitXor] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_BitXor();
            _dispatchTable[(byte)Opcode.BitNot] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_BitNot();
            _dispatchTable[(byte)Opcode.BitShiftLeft] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_BitShiftLeft();
            _dispatchTable[(byte)Opcode.BitShiftRight] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_BitShiftRight();
            _dispatchTable[(byte)Opcode.GetVariable] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_GetVariable(p, f, ref pc);
            _dispatchTable[(byte)Opcode.SetVariable] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_SetVariable(p, f, ref pc);
        }

        public List<DreamValue> Stack { get; } = new(128);
        public Stack<CallFrame> CallStack { get; } = new();

        public DreamProc CurrentProc => CallStack.Peek().Proc;
        public DreamThreadState State { get; private set; } = DreamThreadState.Running;
        public IGameObject? AssociatedObject { get; }

        private readonly DreamVMContext _context;
        private readonly int _maxInstructions;
        private int _totalInstructionsExecuted;

        public DreamThread(DreamProc proc, DreamVMContext context, int maxInstructions, IGameObject? associatedObject = null)
        {
            _context = context;
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
                    var handler = _dispatchTable[(byte)opcode];
                    if (handler != null)
                    {
                        handler(this, ref proc, frame, ref pc);
                        if (opcode == Opcode.Call || opcode == Opcode.Return)
                        {
                            if (State == DreamThreadState.Running)
                            {
                                frame = CallStack.Peek();
                                proc = frame.Proc;
                                pc = frame.PC;
                            }
                        }
                    }
                    else
                    {
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
