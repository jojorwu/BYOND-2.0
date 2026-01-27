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
                    var opcode = (byte)ReadByte(proc, ref pc);
                    var handler = _dispatchTable[opcode];
                    if (handler != null)
                    {
                        handler(this, ref proc, ref pc);
                        // Handlers might have changed the frame (Call/Return)
                        if (State == DreamThreadState.Running)
                        {
                            frame = CallStack.Peek();
                        }
                    }
                    else
                    {
                        State = DreamThreadState.Error;
                        throw new Exception($"Unknown opcode: {(Opcode)opcode}");
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
