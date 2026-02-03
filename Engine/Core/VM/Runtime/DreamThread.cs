using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Core.VM.Procs;
using Shared;
using Shared.Models;

namespace Core.VM.Runtime
{
    public partial class DreamThread : IScriptThread
    {
        private DreamValue[] _stack = new DreamValue[1024];
        private int _stackPtr = 0;
        public Stack<CallFrame> CallStack { get; } = new();
        public Dictionary<int, IEnumerator<DreamValue>> ActiveEnumerators { get; } = new();
        public Dictionary<int, DreamList> EnumeratorLists { get; } = new();
        public int StackCount => _stackPtr;

        public DreamProc CurrentProc => CallStack.Peek().Proc;
        public DreamThreadState State { get; private set; } = DreamThreadState.Running;
        public DateTime SleepUntil { get; private set; }
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

        public DreamThread(DreamThread other, int pc)
        {
            _context = other._context;
            _maxInstructions = other._maxInstructions;
            AssociatedObject = other.AssociatedObject;

            var currentFrame = other.CallStack.Peek();
            CallStack.Push(new CallFrame(currentFrame.Proc, pc, 0, currentFrame.Instance));
        }

        public void Sleep(float seconds)
        {
            State = DreamThreadState.Sleeping;
            SleepUntil = DateTime.Now.AddSeconds(seconds);
        }

        public void Push(DreamValue value)
        {
            if (_stackPtr >= _stack.Length)
            {
                Array.Resize(ref _stack, _stack.Length * 2);
            }
            _stack[_stackPtr++] = value;
        }

        public DreamValue Pop()
        {
            if (_stackPtr <= 0) throw new InvalidOperationException("Stack underflow");
            return _stack[--_stackPtr];
        }

        public DreamValue Peek()
        {
            if (_stackPtr <= 0) throw new InvalidOperationException("Stack is empty");
            return _stack[_stackPtr - 1];
        }

        public DreamValue Peek(int offset)
        {
            if (_stackPtr - offset - 1 < 0) throw new InvalidOperationException("Stack underflow in peek");
            return _stack[_stackPtr - offset - 1];
        }

        public void PopCount(int count)
        {
            if (_stackPtr < count) throw new InvalidOperationException("Stack underflow in popcount");
            _stackPtr -= count;
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
            var value = BinaryPrimitives.ReadInt32LittleEndian(proc.Bytecode.AsSpan(pc));
            pc += 4;
            return value;
        }

        private float ReadSingle(DreamProc proc, ref int pc)
        {
            if (pc + 4 > proc.Bytecode.Length)
                throw new Exception("Attempted to read past the end of the bytecode.");
            var value = BinaryPrimitives.ReadSingleLittleEndian(proc.Bytecode.AsSpan(pc));
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

        private DMReference ReadReference(DreamProc proc, ref int pc)
        {
            var refType = (DMReference.Type)proc.Bytecode[pc++];
            switch (refType)
            {
                case DMReference.Type.Argument:
                case DMReference.Type.Local:
                    return new DMReference { RefType = refType, Index = proc.Bytecode[pc++] };
                case DMReference.Type.Global:
                case DMReference.Type.GlobalProc:
                    var globalIdx = BinaryPrimitives.ReadInt32LittleEndian(proc.Bytecode.AsSpan(pc));
                    pc += 4;
                    return new DMReference { RefType = refType, Index = globalIdx };
                case DMReference.Type.Field:
                case DMReference.Type.SrcProc:
                case DMReference.Type.SrcField:
                    var nameId = BinaryPrimitives.ReadInt32LittleEndian(proc.Bytecode.AsSpan(pc));
                    pc += 4;
                    return new DMReference { RefType = refType, Name = _context.Strings[nameId] };
                default:
                    return new DMReference { RefType = refType };
            }
        }

        private DreamValue GetReferenceValue(DMReference reference, CallFrame frame)
        {
            switch (reference.RefType)
            {
                case DMReference.Type.Src:
                    return frame.Instance != null ? new DreamValue(frame.Instance) : DreamValue.Null;
                case DMReference.Type.Global:
                    return _context.GetGlobal(reference.Index);
                case DMReference.Type.Argument:
                    return _stack[frame.StackBase + reference.Index];
                case DMReference.Type.Local:
                    return _stack[frame.StackBase + frame.Proc.Arguments.Length + reference.Index];
                case DMReference.Type.SrcField:
                    return frame.Instance != null ? frame.Instance.GetVariable(reference.Name) : DreamValue.Null;
                case DMReference.Type.Field:
                    {
                        var obj = _stack[--_stackPtr];
                        if (obj.TryGetValue(out DreamObject? dreamObject) && dreamObject != null)
                            return dreamObject.GetVariable(reference.Name);
                        return DreamValue.Null;
                    }
                case DMReference.Type.ListIndex:
                    {
                        var index = _stack[--_stackPtr];
                        var listValue = _stack[--_stackPtr];
                        if (listValue.TryGetValue(out DreamObject? listObj) && listObj is DreamList list)
                        {
                            if (index.Type == DreamValueType.Float)
                            {
                                int i = (int)index.RawFloat - 1;
                                return (i >= 0 && i < list.Values.Count) ? list.Values[i] : DreamValue.Null;
                            }
                            return list.GetValue(index);
                        }
                        return DreamValue.Null;
                    }
                default:
                    throw new Exception($"Unsupported reference type for reading: {reference.RefType}");
            }
        }

        private void SetReferenceValue(DMReference reference, CallFrame frame, DreamValue value)
        {
            switch (reference.RefType)
            {
                case DMReference.Type.Global:
                    _context.SetGlobal(reference.Index, value);
                    break;
                case DMReference.Type.Argument:
                    _stack[frame.StackBase + reference.Index] = value;
                    break;
                case DMReference.Type.Local:
                    _stack[frame.StackBase + frame.Proc.Arguments.Length + reference.Index] = value;
                    break;
                case DMReference.Type.SrcField:
                    frame.Instance?.SetVariable(reference.Name, value);
                    break;
                case DMReference.Type.Field:
                    {
                        var obj = _stack[--_stackPtr];
                        if (obj.TryGetValue(out DreamObject? dreamObject) && dreamObject != null)
                            dreamObject.SetVariable(reference.Name, value);
                    }
                    break;
                case DMReference.Type.ListIndex:
                    {
                        var index = _stack[--_stackPtr];
                        var listValue = _stack[--_stackPtr];
                        if (listValue.TryGetValue(out DreamObject? listObj) && listObj is DreamList list)
                        {
                            if (index.Type == DreamValueType.Float)
                            {
                                int i = (int)index.RawFloat - 1;
                                if (i >= 0 && i < list.Values.Count)
                                    list.SetValue(i, value);
                                else if (i == list.Values.Count)
                                    list.AddValue(value);
                            }
                            else
                            {
                                list.SetValue(index, value);
                            }
                        }
                    }
                    break;
                default:
                    throw new Exception($"Unsupported reference type for writing: {reference.RefType}");
            }
        }

        public DreamThreadState Run(int instructionBudget)
        {
            if (State == DreamThreadState.Sleeping && DateTime.Now >= SleepUntil)
            {
                State = DreamThreadState.Running;
            }

            if (State != DreamThreadState.Running)
                return State;

            var instructionsExecutedThisTick = 0;
            var frame = CallStack.Peek();
            var proc = frame.Proc;
            var pc = frame.PC;
            var bytecode = proc.Bytecode;

            while (State == DreamThreadState.Running)
            {
                if (instructionsExecutedThisTick++ >= instructionBudget)
                {
                    break;
                }

                if (_totalInstructionsExecuted++ > _maxInstructions)
                {
                    State = DreamThreadState.Error;
                    Console.WriteLine("Error: Instruction limit exceeded.");
                    break;
                }

                // If we've executed past the end of the bytecode, it's an implicit return.
                if (pc >= bytecode.Length)
                {
                    Push(DreamValue.Null);
                    Opcode_Return(ref proc, ref pc);
                    if (State == DreamThreadState.Running && CallStack.Count > 0)
                    {
                        frame = CallStack.Peek();
                        proc = frame.Proc;
                        pc = frame.PC;
                        bytecode = proc.Bytecode;
                    }
                    continue;
                }

                try
                {
                    var opcode = (Opcode)bytecode[pc++];
                    bool potentiallyChangedStack = false;

                    switch (opcode)
                    {
                        case Opcode.PushString:
                            {
                                var stringId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.AsSpan(pc));
                                pc += 4;
                                var val = new DreamValue(_context.Strings[stringId]);
                                if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                _stack[_stackPtr++] = val;
                            }
                            break;
                        case Opcode.PushFloat:
                            {
                                var val = BinaryPrimitives.ReadSingleLittleEndian(bytecode.AsSpan(pc));
                                pc += 4;
                                var dv = new DreamValue(val);
                                if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                _stack[_stackPtr++] = dv;
                            }
                            break;
                        case Opcode.Add:
                            {
                                var b = _stack[--_stackPtr];
                                var a = _stack[_stackPtr - 1];
                                if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                                    _stack[_stackPtr - 1] = new DreamValue(a.RawFloat + b.RawFloat);
                                else
                                    _stack[_stackPtr - 1] = a + b;
                            }
                            break;
                        case Opcode.Subtract:
                            {
                                var b = _stack[--_stackPtr];
                                var a = _stack[_stackPtr - 1];
                                if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                                    _stack[_stackPtr - 1] = new DreamValue(a.RawFloat - b.RawFloat);
                                else
                                    _stack[_stackPtr - 1] = a - b;
                            }
                            break;
                        case Opcode.Multiply:
                            {
                                var b = _stack[--_stackPtr];
                                var a = _stack[_stackPtr - 1];
                                if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                                    _stack[_stackPtr - 1] = new DreamValue(a.RawFloat * b.RawFloat);
                                else
                                    _stack[_stackPtr - 1] = a * b;
                            }
                            break;
                        case Opcode.Divide:
                            {
                                var b = _stack[--_stackPtr];
                                var a = _stack[_stackPtr - 1];
                                if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                                {
                                    var fb = b.RawFloat;
                                    _stack[_stackPtr - 1] = new DreamValue(fb != 0 ? a.RawFloat / fb : 0);
                                }
                                else
                                    _stack[_stackPtr - 1] = a / b;
                            }
                            break;
                        case Opcode.CompareEquals:
                            {
                                var b = _stack[--_stackPtr];
                                _stack[_stackPtr - 1] = _stack[_stackPtr - 1] == b ? DreamValue.True : DreamValue.False;
                            }
                            break;
                        case Opcode.CompareNotEquals:
                            {
                                var b = _stack[--_stackPtr];
                                _stack[_stackPtr - 1] = _stack[_stackPtr - 1] != b ? DreamValue.True : DreamValue.False;
                            }
                            break;
                        case Opcode.CompareLessThan:
                            {
                                var b = _stack[--_stackPtr];
                                _stack[_stackPtr - 1] = _stack[_stackPtr - 1] < b ? DreamValue.True : DreamValue.False;
                            }
                            break;
                        case Opcode.CompareGreaterThan:
                            {
                                var b = _stack[--_stackPtr];
                                _stack[_stackPtr - 1] = _stack[_stackPtr - 1] > b ? DreamValue.True : DreamValue.False;
                            }
                            break;
                        case Opcode.CompareLessThanOrEqual:
                            {
                                var b = _stack[--_stackPtr];
                                _stack[_stackPtr - 1] = _stack[_stackPtr - 1] <= b ? DreamValue.True : DreamValue.False;
                            }
                            break;
                        case Opcode.CompareGreaterThanOrEqual:
                            {
                                var b = _stack[--_stackPtr];
                                _stack[_stackPtr - 1] = _stack[_stackPtr - 1] >= b ? DreamValue.True : DreamValue.False;
                            }
                            break;
                        case Opcode.Negate: _stack[_stackPtr - 1] = -_stack[_stackPtr - 1]; break;
                        case Opcode.BooleanNot: _stack[_stackPtr - 1] = _stack[_stackPtr - 1].IsFalse() ? DreamValue.True : DreamValue.False; break;
                        case Opcode.PushNull:
                            {
                                if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                _stack[_stackPtr++] = DreamValue.Null;
                            }
                            break;
                        case Opcode.Pop: _stackPtr--; break;
                        case Opcode.Call:
                            {
                                var reference = ReadReference(proc, ref pc);
                                var argType = (DMCallArgumentsType)bytecode[pc++];
                                var argStackDelta = BinaryPrimitives.ReadInt32LittleEndian(bytecode.AsSpan(pc));
                                pc += 4;
                                var unusedStackDelta = BinaryPrimitives.ReadInt32LittleEndian(bytecode.AsSpan(pc));
                                pc += 4;

                                PerformCall(reference, argType, argStackDelta);
                                potentiallyChangedStack = true;
                            }
                            break;
                        case Opcode.CallStatement:
                            {
                                var argType = (DMCallArgumentsType)bytecode[pc++];
                                var argStackDelta = BinaryPrimitives.ReadInt32LittleEndian(bytecode.AsSpan(pc));
                                pc += 4;
                                _stackPtr -= argStackDelta;
                                if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                _stack[_stackPtr++] = DreamValue.Null;
                            }
                            break;
                        case Opcode.PushProc:
                            {
                                var procId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.AsSpan(pc));
                                pc += 4;
                                if (procId >= 0 && procId < _context.AllProcs.Count)
                                {
                                    if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                    _stack[_stackPtr++] = new DreamValue((IDreamProc)_context.AllProcs[procId]);
                                }
                                else
                                {
                                    if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                    _stack[_stackPtr++] = DreamValue.Null;
                                }
                            }
                            break;
                        case Opcode.Jump:
                            {
                                pc = BinaryPrimitives.ReadInt32LittleEndian(bytecode.AsSpan(pc));
                            }
                            break;
                        case Opcode.JumpIfFalse:
                            {
                                var val = _stack[--_stackPtr];
                                var address = BinaryPrimitives.ReadInt32LittleEndian(bytecode.AsSpan(pc));
                                if (val.IsFalse())
                                    pc = address;
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.Output:
                            {
                                Console.WriteLine(_stack[--_stackPtr].ToString());
                            }
                            break;
                        case Opcode.Return: Opcode_Return(ref proc, ref pc); potentiallyChangedStack = true; break;
                        case Opcode.BitAnd:
                            {
                                var b = _stack[--_stackPtr];
                                _stack[_stackPtr - 1] &= b;
                            }
                            break;
                        case Opcode.BitOr:
                            {
                                var b = _stack[--_stackPtr];
                                _stack[_stackPtr - 1] |= b;
                            }
                            break;
                        case Opcode.BitXor:
                            {
                                var b = _stack[--_stackPtr];
                                _stack[_stackPtr - 1] ^= b;
                            }
                            break;
                        case Opcode.BitXorReference: Opcode_BitXorReference(proc, frame, ref pc); break;
                        case Opcode.BitNot: _stack[_stackPtr - 1] = ~_stack[_stackPtr - 1]; break;
                        case Opcode.BitShiftLeft:
                            {
                                var b = _stack[--_stackPtr];
                                _stack[_stackPtr - 1] <<= b;
                            }
                            break;
                        case Opcode.BitShiftLeftReference: Opcode_BitShiftLeftReference(proc, frame, ref pc); break;
                        case Opcode.BitShiftRight:
                            {
                                var b = _stack[--_stackPtr];
                                _stack[_stackPtr - 1] >>= b;
                            }
                            break;
                        case Opcode.BitShiftRightReference: Opcode_BitShiftRightReference(proc, frame, ref pc); break;
                        case Opcode.GetVariable:
                            {
                                var nameId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.AsSpan(pc));
                                pc += 4;
                                var instance = frame.Instance;
                                DreamValue val = instance != null ? instance.GetVariable(_context.Strings[nameId]) : DreamValue.Null;
                                if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                _stack[_stackPtr++] = val;
                            }
                            break;
                        case Opcode.SetVariable:
                            {
                                var nameId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.AsSpan(pc));
                                pc += 4;
                                var val = _stack[--_stackPtr];
                                frame.Instance?.SetVariable(_context.Strings[nameId], val);
                            }
                            break;
                        case Opcode.PushReferenceValue:
                            {
                                var refType = (DMReference.Type)bytecode[pc++];
                                switch (refType)
                                {
                                    case DMReference.Type.Local:
                                        {
                                            var val = _stack[frame.StackBase + frame.Proc.Arguments.Length + bytecode[pc++]];
                                            if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                            _stack[_stackPtr++] = val;
                                        }
                                        break;
                                    case DMReference.Type.Argument:
                                        {
                                            var val = _stack[frame.StackBase + bytecode[pc++]];
                                            if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                            _stack[_stackPtr++] = val;
                                        }
                                        break;
                                    case DMReference.Type.Global:
                                        {
                                            var val = _context.GetGlobal(BinaryPrimitives.ReadInt32LittleEndian(bytecode.AsSpan(pc)));
                                            pc += 4;
                                            if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                            _stack[_stackPtr++] = val;
                                        }
                                        break;
                                    case DMReference.Type.Src:
                                        {
                                            var val = frame.Instance != null ? new DreamValue(frame.Instance) : DreamValue.Null;
                                            if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                            _stack[_stackPtr++] = val;
                                        }
                                        break;
                                    default:
                                        {
                                            pc--;
                                            var reference = ReadReference(proc, ref pc);
                                            var val = GetReferenceValue(reference, frame);
                                            if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                            _stack[_stackPtr++] = val;
                                        }
                                        break;
                                }
                            }
                            break;
                        case Opcode.Assign:
                            {
                                var refType = (DMReference.Type)bytecode[pc++];
                                var value = _stack[_stackPtr - 1];
                                switch (refType)
                                {
                                    case DMReference.Type.Local:
                                        _stack[frame.StackBase + frame.Proc.Arguments.Length + bytecode[pc++]] = value;
                                        break;
                                    case DMReference.Type.Argument:
                                        _stack[frame.StackBase + bytecode[pc++]] = value;
                                        break;
                                    case DMReference.Type.Global:
                                        _context.SetGlobal(BinaryPrimitives.ReadInt32LittleEndian(bytecode.AsSpan(pc)), value);
                                        pc += 4;
                                        break;
                                    default:
                                        pc--;
                                        var reference = ReadReference(proc, ref pc);
                                        SetReferenceValue(reference, frame, value);
                                        break;
                                }
                            }
                            break;
                        case Opcode.PushGlobalVars: Opcode_PushGlobalVars(); break;
                        case Opcode.IsNull:
                            {
                                _stack[_stackPtr - 1] = _stack[_stackPtr - 1].IsNull ? DreamValue.True : DreamValue.False;
                            }
                            break;
                        case Opcode.JumpIfNull:
                            {
                                var val = _stack[--_stackPtr];
                                var address = BinaryPrimitives.ReadInt32LittleEndian(bytecode.AsSpan(pc));
                                if (val.Type == DreamValueType.Null)
                                    pc = address;
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.JumpIfNullNoPop:
                            {
                                var val = _stack[_stackPtr - 1];
                                var address = BinaryPrimitives.ReadInt32LittleEndian(bytecode.AsSpan(pc));
                                if (val.Type == DreamValueType.Null)
                                    pc = address;
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.SwitchCase:
                            {
                                var caseValue = _stack[--_stackPtr];
                                var switchValue = _stack[_stackPtr - 1];
                                var jumpAddress = BinaryPrimitives.ReadInt32LittleEndian(bytecode.AsSpan(pc));
                                if (switchValue == caseValue)
                                    pc = jumpAddress;
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.SwitchCaseRange:
                            {
                                var max = _stack[--_stackPtr];
                                var min = _stack[--_stackPtr];
                                var switchValue = _stack[_stackPtr - 1];
                                var jumpAddress = BinaryPrimitives.ReadInt32LittleEndian(bytecode.AsSpan(pc));
                                if (switchValue >= min && switchValue <= max)
                                    pc = jumpAddress;
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.BooleanAnd:
                            {
                                var val = _stack[--_stackPtr];
                                var jumpAddress = BinaryPrimitives.ReadInt32LittleEndian(bytecode.AsSpan(pc));
                                if (val.IsFalse())
                                {
                                    Push(val);
                                    pc = jumpAddress;
                                }
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.BooleanOr:
                            {
                                var val = _stack[--_stackPtr];
                                var jumpAddress = BinaryPrimitives.ReadInt32LittleEndian(bytecode.AsSpan(pc));
                                if (!val.IsFalse())
                                {
                                    Push(val);
                                    pc = jumpAddress;
                                }
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.Increment: Opcode_Increment(proc, frame, ref pc); break;
                        case Opcode.Decrement: Opcode_Decrement(proc, frame, ref pc); break;
                        case Opcode.Modulus: Opcode_Modulus(); break;
                        case Opcode.AssignInto: Opcode_AssignInto(proc, frame, ref pc); break;
                        case Opcode.ModulusReference: Opcode_ModulusReference(proc, frame, ref pc); break;
                        case Opcode.ModulusModulus: Opcode_ModulusModulus(); break;
                        case Opcode.ModulusModulusReference: Opcode_ModulusModulusReference(proc, frame, ref pc); break;
                        case Opcode.CreateList: Opcode_CreateList(proc, ref pc); break;
                        case Opcode.CreateAssociativeList: Opcode_CreateAssociativeList(proc, ref pc); break;
                        case Opcode.CreateStrictAssociativeList: Opcode_CreateStrictAssociativeList(proc, ref pc); break;
                        case Opcode.IsInList: Opcode_IsInList(); break;
                        case Opcode.PickUnweighted: Opcode_PickUnweighted(proc, ref pc); break;
                        case Opcode.PickWeighted: Opcode_PickWeighted(proc, ref pc); break;
                        case Opcode.DereferenceField:
                            {
                                var nameId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.AsSpan(pc));
                                pc += 4;
                                var objValue = _stack[--_stackPtr];
                                DreamValue val = DreamValue.Null;
                                if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
                                    val = obj.GetVariable(_context.Strings[nameId]);
                                if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                _stack[_stackPtr++] = val;
                            }
                            break;
                        case Opcode.DereferenceIndex:
                            {
                                var index = _stack[--_stackPtr];
                                var objValue = _stack[--_stackPtr];
                                DreamValue val = DreamValue.Null;
                                if (objValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
                                {
                                    if (index.Type == DreamValueType.Float)
                                    {
                                        int i = (int)index.RawFloat - 1;
                                        if (i >= 0 && i < list.Values.Count) val = list.Values[i];
                                    }
                                    else val = list.GetValue(index);
                                }
                                if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                _stack[_stackPtr++] = val;
                            }
                            break;
                        case Opcode.DereferenceCall:
                            {
                                var nameId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.AsSpan(pc));
                                pc += 4;
                                var argType = (DMCallArgumentsType)bytecode[pc++];
                                var argStackDelta = BinaryPrimitives.ReadInt32LittleEndian(bytecode.AsSpan(pc));
                                pc += 4;

                                var objValue = _stack[_stackPtr - argStackDelta];
                                if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
                                {
                                    var targetProc = obj.ObjectType.GetProc(_context.Strings[nameId]);
                                    if (targetProc != null)
                                    {
                                        PerformCall(targetProc, obj, argStackDelta, argStackDelta - 1);
                                        potentiallyChangedStack = true;
                                        break;
                                    }
                                }

                                _stackPtr -= argStackDelta;
                                Push(DreamValue.Null);
                                potentiallyChangedStack = true;
                            }
                            break;
                        case Opcode.Initial:
                            {
                                var key = _stack[--_stackPtr];
                                var objValue = _stack[--_stackPtr];

                                if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
                                {
                                    if (key.TryGetValue(out string? varName) && varName != null)
                                    {
                                        int index = obj.ObjectType.GetVariableIndex(varName);
                                        if (index != -1 && index < obj.ObjectType.FlattenedDefaultValues.Count)
                                        {
                                            if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                            _stack[_stackPtr++] = DreamValue.FromObject(obj.ObjectType.FlattenedDefaultValues[index]);
                                            break;
                                        }
                                    }
                                }
                                if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                _stack[_stackPtr++] = DreamValue.Null;
                            }
                            break;
                        case Opcode.IsType:
                            {
                                var typeValue = _stack[--_stackPtr];
                                var objValue = _stack[--_stackPtr];

                                if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj != null &&
                                    typeValue.Type == DreamValueType.DreamType && typeValue.TryGetValue(out ObjectType? type) && type != null)
                                {
                                    if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                    _stack[_stackPtr++] = new DreamValue(obj.ObjectType.IsSubtypeOf(type) ? 1 : 0);
                                }
                                else
                                {
                                    if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                    _stack[_stackPtr++] = new DreamValue(0);
                                }
                            }
                            break;
                        case Opcode.AsType:
                            {
                                var typeValue = _stack[--_stackPtr];
                                var objValue = _stack[--_stackPtr];

                                if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj != null &&
                                    typeValue.Type == DreamValueType.DreamType && typeValue.TryGetValue(out ObjectType? type) && type != null)
                                {
                                    if (obj.ObjectType.IsSubtypeOf(type))
                                    {
                                        if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                        _stack[_stackPtr++] = objValue;
                                    }
                                    else
                                    {
                                        if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                        _stack[_stackPtr++] = DreamValue.Null;
                                    }
                                }
                                else
                                {
                                    if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                    _stack[_stackPtr++] = DreamValue.Null;
                                }
                            }
                            break;
                        case Opcode.CreateListEnumerator: Opcode_CreateListEnumerator(proc, ref pc); break;
                        case Opcode.Enumerate: Opcode_Enumerate(proc, frame, ref pc); break;
                        case Opcode.EnumerateAssoc: Opcode_EnumerateAssoc(proc, frame, ref pc); break;
                        case Opcode.DestroyEnumerator: Opcode_DestroyEnumerator(proc, ref pc); break;
                        case Opcode.Append: Opcode_Append(proc, frame, ref pc); break;
                        case Opcode.Remove: Opcode_Remove(proc, frame, ref pc); break;
                        case Opcode.Prob: Opcode_Prob(); break;
                        case Opcode.GetStep: Opcode_GetStep(); break;
                        case Opcode.GetStepTo: Opcode_GetStepTo(); break;
                        case Opcode.GetDist: Opcode_GetDist(); break;
                        case Opcode.GetDir: Opcode_GetDir(); break;
                        case Opcode.MassConcatenation: Opcode_MassConcatenation(proc, ref pc); break;
                        case Opcode.FormatString: Opcode_FormatString(proc, ref pc); break;
                        case Opcode.Power: Opcode_Power(); break;
                        case Opcode.Sqrt: Opcode_Sqrt(); break;
                        case Opcode.Abs: Opcode_Abs(); break;
                        case Opcode.MultiplyReference: Opcode_MultiplyReference(proc, frame, ref pc); break;
                        case Opcode.Sin: Opcode_Sin(); break;
                        case Opcode.DivideReference: Opcode_DivideReference(proc, frame, ref pc); break;
                        case Opcode.Cos: Opcode_Cos(); break;
                        case Opcode.Tan: Opcode_Tan(); break;
                        case Opcode.ArcSin: Opcode_ArcSin(); break;
                        case Opcode.ArcCos: Opcode_ArcCos(); break;
                        case Opcode.ArcTan: Opcode_ArcTan(); break;
                        case Opcode.ArcTan2: Opcode_ArcTan2(); break;
                        case Opcode.PushType:
                            {
                                var typeId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.AsSpan(pc));
                                pc += 4;
                                var type = _context.ObjectTypeManager?.GetObjectType(typeId);
                                if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                if (type != null)
                                {
                                    _stack[_stackPtr++] = new DreamValue(type);
                                }
                                else
                                {
                                    _stack[_stackPtr++] = DreamValue.Null;
                                }
                            }
                            break;
                        case Opcode.CreateObject: Opcode_CreateObject(proc, ref pc); potentiallyChangedStack = true; break;
                        case Opcode.LocateCoord: Opcode_LocateCoord(); break;
                        case Opcode.Locate: Opcode_Locate(); break;
                        case Opcode.Length: Opcode_Length(); break;
                        case Opcode.Throw: Opcode_Throw(); break;
                        case Opcode.Spawn: Opcode_Spawn(proc, ref pc); break;
                        case Opcode.Rgb: Opcode_Rgb(proc, ref pc); break;
                        case Opcode.Gradient: Opcode_Gradient(proc, ref pc); break;

                        // Optimized opcodes
                        case Opcode.AppendNoPush: Opcode_AppendNoPush(proc, frame, ref pc); break;
                        case Opcode.AssignNoPush: Opcode_AssignNoPush(proc, frame, ref pc); break;
                        case Opcode.PushRefAndDereferenceField: Opcode_PushRefAndDereferenceField(proc, frame, ref pc); break;
                        case Opcode.PushNRefs: Opcode_PushNRefs(proc, frame, ref pc); break;
                        case Opcode.PushNFloats: Opcode_PushNFloats(proc, ref pc); break;
                        case Opcode.PushStringFloat: Opcode_PushStringFloat(proc, ref pc); break;
                        case Opcode.SwitchOnFloat: Opcode_SwitchOnFloat(proc, ref pc); break;
                        case Opcode.SwitchOnString: Opcode_SwitchOnString(proc, ref pc); break;
                        case Opcode.JumpIfReferenceFalse: Opcode_JumpIfReferenceFalse(proc, frame, ref pc); break;
                        case Opcode.ReturnFloat: Opcode_ReturnFloat(proc, ref pc); potentiallyChangedStack = true; break;
                        case Opcode.NPushFloatAssign: Opcode_NPushFloatAssign(proc, frame, ref pc); break;
                        case Opcode.IsTypeDirect: Opcode_IsTypeDirect(proc, ref pc); break;
                        case Opcode.NullRef: Opcode_NullRef(proc, frame, ref pc); break;
                        case Opcode.IndexRefWithString: Opcode_IndexRefWithString(proc, frame, ref pc); break;

                        default:
                            State = DreamThreadState.Error;
                            throw new Exception($"Unknown opcode: {opcode}");
                    }

                    if (potentiallyChangedStack)
                    {
                        if (State == DreamThreadState.Running && CallStack.Count > 0)
                        {
                            frame = CallStack.Peek();
                            proc = frame.Proc;
                            pc = frame.PC;
                            bytecode = proc.Bytecode;
                        }
                    }
                }
                catch (Exception e)
                {
                    State = DreamThreadState.Error;
                    Console.WriteLine($"Error during script execution: {e.Message}");
                    break;
                }
            }

            if (CallStack.Count > 0)
            {
                SavePC(pc);
            }

            return State;
        }
    }
}
