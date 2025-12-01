using System;
using System.Collections.Generic;
using Core.VM.Opcodes;
using Core.VM.Procs;
using Core.VM.Types;

namespace Core.VM.Runtime
{
    public class DreamThread
    {
        public List<DreamValue> Stack { get; } = new();
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
        private readonly Action[] _opcodeHandlers;

        public DreamThread(DreamProc proc, DreamVM vm, int maxInstructions)
        {
            _vm = vm;
            _maxInstructions = maxInstructions;
            _opcodeHandlers = new Action[256]; // Assuming opcodes are bytes
            RegisterOpcodes();

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

                var opcode = ReadByte();
                var handler = _opcodeHandlers[opcode];
                if (handler != null)
                {
                    handler();
                    if (State != DreamThreadState.Running) // Check state after handler execution
                        return State;
                }
                else
                {
                    State = DreamThreadState.Error;
                    throw new Exception($"Unknown opcode: (Opcode)0x{opcode:X2}");
                }
            }

            State = DreamThreadState.Finished;
            return State;
        }

        private void RegisterOpcodes()
        {
            _opcodeHandlers[(int)Opcode.PushString] = Opcode_PushString;
            _opcodeHandlers[(int)Opcode.PushFloat] = Opcode_PushFloat;
            _opcodeHandlers[(int)Opcode.Add] = Opcode_Add;
            _opcodeHandlers[(int)Opcode.Subtract] = Opcode_Subtract;
            _opcodeHandlers[(int)Opcode.Multiply] = Opcode_Multiply;
            _opcodeHandlers[(int)Opcode.Divide] = Opcode_Divide;
            _opcodeHandlers[(int)Opcode.CompareEquals] = Opcode_CompareEquals;
            _opcodeHandlers[(int)Opcode.CompareNotEquals] = Opcode_CompareNotEquals;
            _opcodeHandlers[(int)Opcode.Call] = Opcode_Call;
            _opcodeHandlers[(int)Opcode.Jump] = Opcode_Jump;
            _opcodeHandlers[(int)Opcode.JumpIfFalse] = Opcode_JumpIfFalse;
            _opcodeHandlers[(int)Opcode.Output] = Opcode_Output;
            _opcodeHandlers[(int)Opcode.Return] = Opcode_Return;
            _opcodeHandlers[(int)Opcode.PushArgument] = Opcode_PushArgument;
            _opcodeHandlers[(int)Opcode.SetArgument] = Opcode_SetArgument;
            _opcodeHandlers[(int)Opcode.PushLocal] = Opcode_PushLocal;
            _opcodeHandlers[(int)Opcode.SetLocal] = Opcode_SetLocal;
        }

        #region Opcode Handlers
        private void Opcode_PushString()
        {
            var stringId = ReadInt32();
            Push(new DreamValue(_vm.Strings[stringId]));
        }

        private void Opcode_PushFloat()
        {
            var value = ReadSingle();
            Push(new DreamValue(value));
        }

        private void Opcode_Add()
        {
            var b = Pop();
            var a = Pop();
            Push(a + b);
        }

        private void Opcode_Subtract()
        {
            var b = Pop();
            var a = Pop();
            Push(a - b);
        }

        private void Opcode_Multiply()
        {
            var b = Pop();
            var a = Pop();
            Push(a * b);
        }

        private void Opcode_Divide()
        {
            var b = Pop();
            var a = Pop();
            Push(a / b);
        }

        private void Opcode_CompareEquals()
        {
            var b = Pop();
            var a = Pop();
            Push(new DreamValue(a == b ? 1 : 0));
        }

        private void Opcode_CompareNotEquals()
        {
            var b = Pop();
            var a = Pop();
            Push(new DreamValue(a != b ? 1 : 0));
        }

        private void Opcode_Call()
        {
            var procId = ReadInt32();
            var procName = _vm.Strings[procId];
            var proc = _vm.Procs[procName];

            // The current PC (return address) is already stored in the current frame.
            // We just need to create the new frame and push it.
            var frame = new CallFrame(proc, PC, Stack.Count);
            CallStack.Push(frame);
            // PC is now automatically pointing to the new frame's PC (which is 0)
        }

        private void Opcode_Jump()
        {
            var address = ReadInt32();
            PC = address;
        }

        private void Opcode_JumpIfFalse()
        {
            var value = Pop();
            var address = ReadInt32();
            if (value.IsFalse())
                PC = address;
        }

        private void Opcode_Output()
        {
            var value = Pop();
            Console.WriteLine(value.ToString());
        }

        private void Opcode_Return()
        {
            var returnedFrame = CallStack.Pop();
            if (CallStack.Count > 0)
            {
                // Set the PC of the *new* top frame to the return address we stored.
                PC = returnedFrame.ReturnAddress;
            }
            else
            {
                // We've returned from the last proc, thread is finished.
                State = DreamThreadState.Finished;
            }
        }

        private void Opcode_PushArgument()
        {
            var argIndex = ReadByte();
            var frame = CallStack.Peek();
            Push(Stack[frame.StackBase + argIndex]);
        }

        private void Opcode_SetArgument()
        {
            var argIndex = ReadByte();
            var value = Pop();
            var frame = CallStack.Peek();
            Stack[frame.StackBase + argIndex] = value;
        }

        private void Opcode_PushLocal()
        {
            var localIndex = ReadByte();
            var frame = CallStack.Peek();
            Push(Stack[frame.StackBase + frame.Proc.Arguments.Length + localIndex]);
        }

        private void Opcode_SetLocal()
        {
            var localIndex = ReadByte();
            var value = Pop();
            var frame = CallStack.Peek();
            Stack[frame.StackBase + frame.Proc.Arguments.Length + localIndex] = value;
        }
        #endregion
    }
}
