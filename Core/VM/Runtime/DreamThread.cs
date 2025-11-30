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
        public DreamThreadState State { get; private set; } = DreamThreadState.Running;

        private readonly DreamVM _vm;
        private readonly int _maxInstructions;
        private readonly Stack<CallFrame> _callStack = new();
        private int _totalInstructionsExecuted;

        public DreamThread(DreamProc proc, DreamVM vm, int maxInstructions)
        {
            _vm = vm;
            _maxInstructions = maxInstructions;
            _callStack.Push(new CallFrame(proc));
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
            var frame = _callStack.Peek();
            if (frame.PC + 1 > frame.Proc.Bytecode.Length)
                throw new Exception("Attempted to read past the end of the bytecode.");
            return frame.Proc.Bytecode[frame.PC++];
        }

        private int ReadInt32()
        {
            var frame = _callStack.Peek();
            if (frame.PC + 4 > frame.Proc.Bytecode.Length)
                throw new Exception("Attempted to read past the end of the bytecode.");
            var value = BitConverter.ToInt32(frame.Proc.Bytecode, frame.PC);
            frame.PC += 4;
            return value;
        }

        private float ReadSingle()
        {
            var frame = _callStack.Peek();
            if (frame.PC + 4 > frame.Proc.Bytecode.Length)
                throw new Exception("Attempted to read past the end of the bytecode.");
            var value = BitConverter.ToSingle(frame.Proc.Bytecode, frame.PC);
            frame.PC += 4;
            return value;
        }

        public DreamThreadState Run(int instructionBudget)
        {
            if (State != DreamThreadState.Running)
                return State;

            var instructionsExecutedThisTick = 0;
            while (_callStack.Count > 0)
            {
                var frame = _callStack.Peek();
                if (frame.PC >= frame.Proc.Bytecode.Length)
                {
                    _callStack.Pop();
                    continue;
                }

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
                    case Opcode.Jump:
                    {
                        var address = ReadInt32();
                        _callStack.Peek().PC = address;
                        break;
                    }
                    case Opcode.JumpIfFalse:
                    {
                        var value = Pop();
                        var address = ReadInt32();
                        if (value.IsFalse())
                        {
                            _callStack.Peek().PC = address;
                        }
                        break;
                    }
                    case Opcode.PushProc:
                    {
                        var procId = ReadInt32();
                        var procName = _vm.Strings[procId];
                        if (_vm.Procs.TryGetValue(procName, out var proc))
                        {
                            Push(new DreamValue(proc));
                        }
                        else
                        {
                            throw new Exception($"Proc '{procName}' not found.");
                        }
                        break;
                    }
                    case Opcode.Call:
                    {
                        var procValue = Pop();
                        if (procValue.TryGetValue(out DreamProc? proc))
                        {
                            _callStack.Push(new CallFrame(proc));
                        }
                        else
                        {
                            throw new Exception("Attempted to call a non-proc value.");
                        }
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
                        _callStack.Pop();
                        if (_callStack.Count == 0)
                        {
                            State = DreamThreadState.Finished;
                            return State;
                        }
                        break;
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
