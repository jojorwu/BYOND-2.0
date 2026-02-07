using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Core.VM.Procs;
using Shared;


namespace Core.VM.Runtime
{
    public struct TryBlock
    {
        public int CatchAddress;
        public int CallStackDepth;
        public int StackPointer;
        public DMReference? CatchReference;
    }

    /// <summary>
    /// Represents a single thread of execution within the Dream VM.
    /// Manages its own stack, call frames, and execution state.
    ///
    /// The VM uses a stack-based architecture where most opcodes operate on values
    /// at the top of the stack. High-frequency opcodes are inlined directly into the
    /// Run() loop to minimize overhead.
    /// </summary>
    public partial class DreamThread : IScriptThread
    {
        private DreamValue[] _stack = new DreamValue[1024];
        private int _stackPtr = 0;
        public Stack<CallFrame> CallStack { get; } = new();
        public Stack<TryBlock> TryStack { get; } = new();
        public Dictionary<int, IEnumerator<DreamValue>> ActiveEnumerators { get; } = new();
        public Dictionary<int, DreamList> EnumeratorLists { get; } = new();
        public int StackCount => _stackPtr;

        public DreamProc CurrentProc => CallStack.Peek().Proc;
        public DreamThreadState State { get; private set; } = DreamThreadState.Running;
        public DateTime SleepUntil { get; private set; }
        public IGameObject? AssociatedObject { get; }
        public DreamObject? Usr { get; set; }

        public DreamVMContext Context { get; }
        private readonly int _maxInstructions;
        private int _totalInstructionsExecuted;

        public DreamThread(DreamProc proc, DreamVMContext context, int maxInstructions, IGameObject? associatedObject = null)
        {
            Context = context;
            _maxInstructions = maxInstructions;
            AssociatedObject = associatedObject;

            CallStack.Push(new CallFrame(proc, 0, 0, associatedObject as DreamObject));
        }

        public DreamThread(DreamThread other, int pc)
        {
            Context = other.Context;
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
            if (_stackPtr <= 0) throw new ScriptRuntimeException("Stack underflow during Pop", CurrentProc, CallStack.Peek().PC, CallStack);
            return _stack[--_stackPtr];
        }

        public DreamValue Peek()
        {
            if (_stackPtr <= 0) throw new ScriptRuntimeException("Stack underflow during Peek", CurrentProc, CallStack.Peek().PC, CallStack);
            return _stack[_stackPtr - 1];
        }

        public DreamValue Peek(int offset)
        {
            if (_stackPtr - offset - 1 < 0) throw new ScriptRuntimeException($"Stack underflow during Peek({offset})", CurrentProc, CallStack.Peek().PC, CallStack);
            return _stack[_stackPtr - offset - 1];
        }

        public void PopCount(int count)
        {
            if (_stackPtr < count) throw new ScriptRuntimeException($"Stack underflow during PopCount({count})", CurrentProc, CallStack.Peek().PC, CallStack);
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

        private bool HandleException(ScriptRuntimeException e)
        {
            if (TryStack.Count > 0)
            {
                var tryBlock = TryStack.Pop();

                // Unwind CallStack
                while (CallStack.Count > tryBlock.CallStackDepth)
                {
                    CallStack.Pop();
                }

                // Restore stack pointer
                _stackPtr = tryBlock.StackPointer;

                // Set catch variable if needed
                if (tryBlock.CatchReference.HasValue)
                {
                    var catchValue = e.ThrownValue ?? new DreamValue(e.Message);
                    SetReferenceValue(tryBlock.CatchReference.Value, CallStack.Peek(), catchValue, 0);
                    PopCount(GetReferenceStackSize(tryBlock.CatchReference.Value));
                }

                // Jump to catch address
                var frame = CallStack.Peek();
                frame.PC = tryBlock.CatchAddress;
                CallStack.Pop();
                CallStack.Push(frame);

                State = DreamThreadState.Running;
                return true;
            }

            State = DreamThreadState.Error;
            Console.WriteLine(e.ToString());
            return false;
        }

        /// <summary>
        /// Reads a reference from the bytecode stream.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private DMReference ReadReference(ReadOnlySpan<byte> bytecode, ref int pc)
        {
            var refType = (DMReference.Type)bytecode[pc++];
            switch (refType)
            {
                case DMReference.Type.Argument:
                case DMReference.Type.Local:
                    return new DMReference { RefType = refType, Index = bytecode[pc++] };
                case DMReference.Type.Global:
                case DMReference.Type.GlobalProc:
                    var globalIdx = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                    pc += 4;
                    return new DMReference { RefType = refType, Index = globalIdx };
                case DMReference.Type.Field:
                case DMReference.Type.SrcProc:
                case DMReference.Type.SrcField:
                    var nameId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                    pc += 4;
                    return new DMReference { RefType = refType, Name = Context.Strings[nameId] };
                default:
                    return new DMReference { RefType = refType };
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetReferenceStackSize(DMReference reference)
        {
            return reference.RefType switch
            {
                DMReference.Type.Field => 1,
                DMReference.Type.ListIndex => 2,
                _ => 0
            };
        }

        /// <summary>
        /// Resolves a reference to its current value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private DreamValue GetReferenceValue(DMReference reference, CallFrame frame, int stackOffset = 0)
        {
            switch (reference.RefType)
            {
                case DMReference.Type.NoRef:
                    return DreamValue.Null;
                case DMReference.Type.Src:
                case DMReference.Type.Self:
                    return frame.Instance != null ? new DreamValue(frame.Instance) : DreamValue.Null;
                case DMReference.Type.Usr:
                    return Usr != null ? new DreamValue(Usr) : DreamValue.Null;
                case DMReference.Type.World:
                    return Context.World != null ? new DreamValue(Context.World) : DreamValue.Null;
                case DMReference.Type.Args:
                    {
                        var list = new DreamList(Context.ListType);
                        for (int i = 0; i < frame.Proc.Arguments.Length; i++)
                        {
                            list.AddValue(_stack[frame.StackBase + i]);
                        }
                        return new DreamValue(list);
                    }
                case DMReference.Type.Global:
                    return Context.GetGlobal(reference.Index);
                case DMReference.Type.Argument:
                    return _stack[frame.StackBase + reference.Index];
                case DMReference.Type.Local:
                    return _stack[frame.StackBase + frame.Proc.Arguments.Length + reference.Index];
                case DMReference.Type.SrcField:
                    return frame.Instance != null ? frame.Instance.GetVariable(reference.Name) : DreamValue.Null;
                case DMReference.Type.Field:
                    {
                        var obj = _stack[_stackPtr - 1 - stackOffset];
                        if (obj.TryGetValue(out DreamObject? dreamObject) && dreamObject != null)
                            return dreamObject.GetVariable(reference.Name);
                        return DreamValue.Null;
                    }
                case DMReference.Type.ListIndex:
                    {
                        var index = _stack[_stackPtr - 1 - stackOffset];
                        var listValue = _stack[_stackPtr - 2 - stackOffset];
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

        /// <summary>
        /// Updates the value pointed to by a reference.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetReferenceValue(DMReference reference, CallFrame frame, DreamValue value, int stackOffset = 0)
        {
            switch (reference.RefType)
            {
                case DMReference.Type.Global:
                    Context.SetGlobal(reference.Index, value);
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
                        var obj = _stack[_stackPtr - 1 - stackOffset];
                        if (obj.TryGetValue(out DreamObject? dreamObject) && dreamObject != null)
                            dreamObject.SetVariable(reference.Name, value);
                    }
                    break;
                case DMReference.Type.ListIndex:
                    {
                        var index = _stack[_stackPtr - 1 - stackOffset];
                        var listValue = _stack[_stackPtr - 2 - stackOffset];
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

        /// <summary>
        /// Executes script instructions for this thread until the instruction budget is exhausted
        /// or the thread state changes (e.g., sleeps, finishes, or errors).
        /// </summary>
        /// <param name="instructionBudget">Maximum number of instructions to execute in this call.</param>
        /// <returns>The current state of the thread after execution.</returns>
        public DreamThreadState Run(int instructionBudget)
        {
            // Resume from sleep if the time has passed
            if (State == DreamThreadState.Sleeping && DateTime.Now >= SleepUntil)
            {
                State = DreamThreadState.Running;
            }

            if (State != DreamThreadState.Running)
                return State;

            var instructionsExecutedThisTick = 0;

            // Local cache of the current execution context for high performance
            var frame = CallStack.Peek();
            var proc = frame.Proc;
            var pc = frame.PC;
            ReadOnlySpan<byte> bytecode = proc.Bytecode;

            while (State == DreamThreadState.Running)
            {
                // Check if we've exceeded the per-tick or per-thread instruction budget
                if (instructionsExecutedThisTick++ >= instructionBudget)
                {
                    break;
                }

                if (_totalInstructionsExecuted++ > _maxInstructions)
                {
                    State = DreamThreadState.Error;
                    Console.WriteLine("Error: Total instruction limit exceeded for thread.");
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
                                var stringId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var val = new DreamValue(Context.Strings[stringId]);
                                if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                _stack[_stackPtr++] = val;
                            }
                            break;
                        case Opcode.PushFloat:
                            {
                                var val = BinaryPrimitives.ReadSingleLittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var dv = new DreamValue(val);
                                if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                _stack[_stackPtr++] = dv;
                            }
                            break;
                        case Opcode.PushNull:
                            {
                                if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                _stack[_stackPtr++] = DreamValue.Null;
                            }
                            break;
                        case Opcode.Pop: _stackPtr--; break;

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
                        case Opcode.CompareEquivalent:
                            {
                                var b = Pop();
                                var a = Pop();
                                Push(a.Equals(b) ? DreamValue.True : DreamValue.False);
                            }
                            break;
                        case Opcode.CompareNotEquivalent:
                            {
                                var b = Pop();
                                var a = Pop();
                                Push(!a.Equals(b) ? DreamValue.True : DreamValue.False);
                            }
                            break;
                        case Opcode.Negate: _stack[_stackPtr - 1] = -_stack[_stackPtr - 1]; break;
                        case Opcode.BooleanNot: _stack[_stackPtr - 1] = _stack[_stackPtr - 1].IsFalse() ? DreamValue.True : DreamValue.False; break;
                        case Opcode.Call:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var argType = (DMCallArgumentsType)bytecode[pc++];
                                var argStackDelta = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var unusedStackDelta = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;

                                SavePC(pc);
                                PerformCall(reference, argType, argStackDelta);
                                potentiallyChangedStack = true;
                            }
                            break;
                        case Opcode.CallStatement:
                            {
                                var argType = (DMCallArgumentsType)bytecode[pc++];
                                var argStackDelta = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;

                                var instance = frame.Instance;
                                IDreamProc? parentProc = null;

                                if (instance != null && instance.ObjectType != null)
                                {
                                    ObjectType? definingType = null;
                                    ObjectType? current = instance.ObjectType;
                                    while (current != null)
                                    {
                                        if (current.Procs.ContainsValue(frame.Proc))
                                        {
                                            definingType = current;
                                            break;
                                        }
                                        current = current.Parent;
                                    }

                                    if (definingType != null)
                                    {
                                        parentProc = definingType.Parent?.GetProc(frame.Proc.Name);
                                    }
                                }

                                if (parentProc != null)
                                {
                                    SavePC(pc);
                                    PerformCall(parentProc, instance, argStackDelta, argStackDelta);
                                }
                                else
                                {
                                    _stackPtr -= argStackDelta;
                                    if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                    _stack[_stackPtr++] = DreamValue.Null;
                                }
                                potentiallyChangedStack = true;
                            }
                            break;
                        case Opcode.PushProc:
                            {
                                var procId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                if (procId >= 0 && procId < Context.AllProcs.Count)
                                {
                                    if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                    _stack[_stackPtr++] = new DreamValue((IDreamProc)Context.AllProcs[procId]);
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
                                pc = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                            }
                            break;
                        case Opcode.JumpIfFalse:
                            {
                                var val = _stack[--_stackPtr];
                                var address = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                if (val.IsFalse())
                                    pc = address;
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.JumpIfTrueReference:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var address = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                var val = GetReferenceValue(reference, frame, 0);
                                PopCount(GetReferenceStackSize(reference));
                                if (!val.IsFalse())
                                    pc = address;
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.JumpIfFalseReference:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var address = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                var val = GetReferenceValue(reference, frame, 0);
                                PopCount(GetReferenceStackSize(reference));
                                if (val.IsFalse())
                                    pc = address;
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.Output:
                            {
                                var message = _stack[--_stackPtr];
                                var target = _stack[--_stackPtr];

                                if (!message.IsNull)
                                {
                                    // TODO: Proper output routing based on target
                                    Console.WriteLine(message.ToString());
                                }
                            }
                            break;
                        case Opcode.OutputReference: Opcode_OutputReference(proc, frame, ref pc); break;
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
                        case Opcode.BitXorReference:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = Pop();
                                var refValue = GetReferenceValue(reference, frame, 0);
                                SetReferenceValue(reference, frame, refValue ^ value, 0);
                                PopCount(GetReferenceStackSize(reference));
                            }
                            break;
                        case Opcode.BitNot: _stack[_stackPtr - 1] = ~_stack[_stackPtr - 1]; break;
                        case Opcode.BitShiftLeft:
                            {
                                var b = _stack[--_stackPtr];
                                _stack[_stackPtr - 1] <<= b;
                            }
                            break;
                        case Opcode.BitShiftLeftReference:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = Pop();
                                var refValue = GetReferenceValue(reference, frame, 0);
                                SetReferenceValue(reference, frame, refValue << value, 0);
                                PopCount(GetReferenceStackSize(reference));
                            }
                            break;
                        case Opcode.BitShiftRight:
                            {
                                var b = _stack[--_stackPtr];
                                _stack[_stackPtr - 1] >>= b;
                            }
                            break;
                        case Opcode.BitShiftRightReference:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = Pop();
                                var refValue = GetReferenceValue(reference, frame, 0);
                                SetReferenceValue(reference, frame, refValue >> value, 0);
                                PopCount(GetReferenceStackSize(reference));
                            }
                            break;

                        case Opcode.GetVariable:
                            {
                                var nameId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var instance = frame.Instance;
                                DreamValue val = instance != null ? instance.GetVariable(Context.Strings[nameId]) : DreamValue.Null;
                                if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                _stack[_stackPtr++] = val;
                            }
                            break;
                        case Opcode.SetVariable:
                            {
                                var nameId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var val = _stack[--_stackPtr];
                                frame.Instance?.SetVariable(Context.Strings[nameId], val);
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
                                            var val = Context.GetGlobal(BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc)));
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
                                            var reference = ReadReference(bytecode, ref pc);
                                            var val = GetReferenceValue(reference, frame, 0);
                                            PopCount(GetReferenceStackSize(reference));
                                            Push(val);
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
                                        Context.SetGlobal(BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc)), value);
                                        pc += 4;
                                        break;
                                    default:
                                        pc--;
                                        var reference = ReadReference(bytecode, ref pc);
                                        SetReferenceValue(reference, frame, value, 1);
                                        var val = Pop();
                                        PopCount(GetReferenceStackSize(reference));
                                        Push(val);
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
                                var address = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                if (val.Type == DreamValueType.Null)
                                    pc = address;
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.JumpIfNullNoPop:
                            {
                                var val = _stack[_stackPtr - 1];
                                var address = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
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
                                var jumpAddress = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
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
                                var jumpAddress = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                if (switchValue >= min && switchValue <= max)
                                    pc = jumpAddress;
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.BooleanAnd:
                            {
                                var val = _stack[--_stackPtr];
                                var jumpAddress = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
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
                                var jumpAddress = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                if (!val.IsFalse())
                                {
                                    Push(val);
                                    pc = jumpAddress;
                                }
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.Increment:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = GetReferenceValue(reference, frame, 0);
                                var newValue = value + 1;
                                SetReferenceValue(reference, frame, newValue, 0);
                                PopCount(GetReferenceStackSize(reference));
                                Push(newValue);
                            }
                            break;
                        case Opcode.Decrement:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = GetReferenceValue(reference, frame, 0);
                                var newValue = value - 1;
                                SetReferenceValue(reference, frame, newValue, 0);
                                PopCount(GetReferenceStackSize(reference));
                                Push(newValue);
                            }
                            break;
                        case Opcode.Modulus: Opcode_Modulus(); break;
                        case Opcode.AssignInto:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = Pop();
                                SetReferenceValue(reference, frame, value, 0);
                                PopCount(GetReferenceStackSize(reference));
                                Push(value);
                            }
                            break;
                        case Opcode.ModulusReference:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = Pop();
                                var refValue = GetReferenceValue(reference, frame, 0);
                                SetReferenceValue(reference, frame, refValue % value, 0);
                                PopCount(GetReferenceStackSize(reference));
                            }
                            break;
                        case Opcode.ModulusModulus: Opcode_ModulusModulus(); break;
                        case Opcode.ModulusModulusReference:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = Pop();
                                var refValue = GetReferenceValue(reference, frame, 0);
                                SetReferenceValue(reference, frame, new DreamValue(SharedOperations.Modulo(refValue.GetValueAsFloat(), value.GetValueAsFloat())), 0);
                                PopCount(GetReferenceStackSize(reference));
                            }
                            break;
                        case Opcode.CreateList: Opcode_CreateList(proc, ref pc); break;
                        case Opcode.CreateAssociativeList: Opcode_CreateAssociativeList(proc, ref pc); break;
                        case Opcode.CreateStrictAssociativeList: Opcode_CreateStrictAssociativeList(proc, ref pc); break;
                        case Opcode.IsInList: Opcode_IsInList(); break;
                        case Opcode.Input:
                            {
                                var ref1 = ReadReference(bytecode, ref pc);
                                var ref2 = ReadReference(bytecode, ref pc);
                                PopCount(4); // user, message, title, default_value
                                Push(DreamValue.Null);
                            }
                            break;
                        case Opcode.PickUnweighted: Opcode_PickUnweighted(proc, ref pc); break;
                        case Opcode.PickWeighted: Opcode_PickWeighted(proc, ref pc); break;
                        case Opcode.DereferenceField:
                            {
                                var nameId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var objValue = _stack[--_stackPtr];
                                DreamValue val = DreamValue.Null;
                                if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
                                    val = obj.GetVariable(Context.Strings[nameId]);
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
                                _stack[_stackPtr++] = val;
                            }
                            break;
                        case Opcode.PopReference:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                PopCount(GetReferenceStackSize(reference));
                            }
                            break;
                        case Opcode.DereferenceCall:
                            {
                                var nameId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var argType = (DMCallArgumentsType)bytecode[pc++];
                                var argStackDelta = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;

                                var objValue = _stack[_stackPtr - argStackDelta];
                                if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
                                {
                                    var procName = Context.Strings[nameId];
                                    var targetProc = obj.ObjectType?.GetProc(procName);
                                    if (targetProc == null)
                                    {
                                        var varValue = obj.GetVariable(procName);
                                        if (varValue.TryGetValue(out IDreamProc? procFromVar))
                                        {
                                            targetProc = procFromVar;
                                        }
                                    }

                                    if (targetProc != null)
                                    {
                                        SavePC(pc);
                                        int argCount = argStackDelta - 1;
                                        int stackBase = _stackPtr - argStackDelta;

                                        // Shift arguments down by 1, overwriting 'obj' on the stack
                                        for (int i = 0; i < argCount; i++)
                                        {
                                            _stack[stackBase + i] = _stack[stackBase + i + 1];
                                        }
                                        _stackPtr--; // Effectively removed the object

                                        PerformCall(targetProc, obj, argCount, argCount);
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

                                if (objValue.TryGetValue(out DreamObject? obj) && obj != null && obj.ObjectType != null)
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

                                if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj?.ObjectType != null &&
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

                                if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj?.ObjectType != null &&
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
                        case Opcode.DeleteObject:
                            {
                                var value = Pop();
                                if (value.TryGetValueAsGameObject(out var obj))
                                {
                                    Context.GameState?.RemoveGameObject(obj);
                                }
                            }
                            break;
                        case Opcode.Prob: Opcode_Prob(); break;
                        case Opcode.IsSaved:
                            {
                                Push(DreamValue.True);
                            }
                            break;
                        case Opcode.GetStep: Opcode_GetStep(); break;
                        case Opcode.GetStepTo: Opcode_GetStepTo(); break;
                        case Opcode.GetDist: Opcode_GetDist(); break;
                        case Opcode.GetDir: Opcode_GetDir(); break;
                        case Opcode.MassConcatenation: Opcode_MassConcatenation(proc, ref pc); break;
                        case Opcode.FormatString: Opcode_FormatString(proc, ref pc); break;
                        case Opcode.Power: Opcode_Power(); break;
                        case Opcode.Sqrt: Opcode_Sqrt(); break;
                        case Opcode.Abs: Opcode_Abs(); break;
                        case Opcode.MultiplyReference:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = Pop();
                                var refValue = GetReferenceValue(reference, frame, 0);
                                SetReferenceValue(reference, frame, refValue * value, 0);
                                PopCount(GetReferenceStackSize(reference));
                            }
                            break;
                        case Opcode.Sin: Opcode_Sin(); break;
                        case Opcode.DivideReference:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = Pop();
                                var refValue = GetReferenceValue(reference, frame, 0);
                                SetReferenceValue(reference, frame, refValue / value, 0);
                                PopCount(GetReferenceStackSize(reference));
                            }
                            break;
                        case Opcode.Cos: Opcode_Cos(); break;
                        case Opcode.Tan: Opcode_Tan(); break;
                        case Opcode.ArcSin: Opcode_ArcSin(); break;
                        case Opcode.ArcCos: Opcode_ArcCos(); break;
                        case Opcode.ArcTan: Opcode_ArcTan(); break;
                        case Opcode.ArcTan2: Opcode_ArcTan2(); break;
                        case Opcode.Log:
                            {
                                var baseValue = Pop();
                                var x = Pop();
                                Push(new DreamValue(MathF.Log(x.GetValueAsFloat(), baseValue.GetValueAsFloat())));
                            }
                            break;
                        case Opcode.LogE:
                            {
                                Push(new DreamValue(MathF.Log(Pop().GetValueAsFloat())));
                            }
                            break;
                        case Opcode.PushType:
                            {
                                var typeId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var type = Context.ObjectTypeManager?.GetObjectType(typeId);
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
                        case Opcode.Length:
                            {
                                var value = _stack[--_stackPtr];
                                DreamValue result;

                                if (value.Type == DreamValueType.String && value.TryGetValue(out string? str))
                                {
                                    result = new DreamValue(str?.Length ?? 0);
                                }
                                else if (value.Type == DreamValueType.DreamObject && value.TryGetValue(out DreamObject? obj) && obj is DreamList list)
                                {
                                    result = new DreamValue(list.Values.Count);
                                }
                                else
                                {
                                    result = new DreamValue(0);
                                }

                                Push(result);
                            }
                            break;
                        case Opcode.IsInRange:
                            {
                                var max = _stack[--_stackPtr];
                                var min = _stack[--_stackPtr];
                                var val = _stack[--_stackPtr];
                                if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                _stack[_stackPtr++] = new DreamValue(val >= min && val <= max ? 1 : 0);
                            }
                            break;
                        case Opcode.Throw:
                            {
                                var value = _stack[--_stackPtr];
                                var e = new ScriptRuntimeException(value.ToString(), proc, pc, CallStack);
                                e.ThrownValue = value;
                                HandleException(e);
                                potentiallyChangedStack = true;
                            }
                            break;
                        case Opcode.Try:
                            {
                                var catchAddress = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var catchRef = ReadReference(bytecode, ref pc);
                                TryStack.Push(new TryBlock
                                {
                                    CatchAddress = catchAddress,
                                    CallStackDepth = CallStack.Count,
                                    StackPointer = _stackPtr,
                                    CatchReference = catchRef
                                });
                            }
                            break;
                        case Opcode.TryNoValue:
                            {
                                var catchAddress = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                TryStack.Push(new TryBlock
                                {
                                    CatchAddress = catchAddress,
                                    CallStackDepth = CallStack.Count,
                                    StackPointer = _stackPtr,
                                    CatchReference = null
                                });
                            }
                            break;
                        case Opcode.EndTry:
                            {
                                if (TryStack.Count > 0)
                                    TryStack.Pop();
                            }
                            break;
                        case Opcode.Spawn:
                            {
                                var address = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                var bodyPc = pc + 4;
                                pc = address; // Skip body in main thread

                                var delay = _stack[--_stackPtr];

                                var newThread = new DreamThread(this, bodyPc);
                                if (delay.TryGetValue(out float seconds) && seconds > 0)
                                {
                                    newThread.Sleep(seconds / 10.0f);
                                }
                                Context.ScriptHost?.AddThread(newThread);
                            }
                            break;
                        case Opcode.Rgb: Opcode_Rgb(proc, ref pc); break;
                        case Opcode.Gradient: Opcode_Gradient(proc, ref pc); break;

                        // Optimized opcodes
                        case Opcode.AppendNoPush:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = _stack[--_stackPtr];
                                var listValue = GetReferenceValue(reference, frame);

                                if (listValue.Type == DreamValueType.DreamObject && listValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
                                {
                                    list.AddValue(value);
                                }
                            }
                            break;
                        case Opcode.AssignNoPush:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = _stack[--_stackPtr];
                                SetReferenceValue(reference, frame, value);
                            }
                            break;
                        case Opcode.PushRefAndDereferenceField:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var fieldNameId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var fieldName = Context.Strings[fieldNameId];

                                var objValue = GetReferenceValue(reference, frame);
                                if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj != null)
                                {
                                    _stack[_stackPtr++] = obj.GetVariable(fieldName);
                                }
                                else
                                {
                                    _stack[_stackPtr++] = DreamValue.Null;
                                }
                            }
                            break;
                        case Opcode.PushNRefs:
                            {
                                var count = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                for (int i = 0; i < count; i++)
                                {
                                    var reference = ReadReference(bytecode, ref pc);
                                    if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                    _stack[_stackPtr++] = GetReferenceValue(reference, frame);
                                }
                            }
                            break;
                        case Opcode.PushNFloats:
                            {
                                var count = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                for (int i = 0; i < count; i++)
                                {
                                    if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                    _stack[_stackPtr++] = new DreamValue(BinaryPrimitives.ReadSingleLittleEndian(bytecode.Slice(pc)));
                                    pc += 4;
                                }
                            }
                            break;
                        case Opcode.PushStringFloat:
                            {
                                var stringId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var value = BinaryPrimitives.ReadSingleLittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                if (_stackPtr + 1 >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                _stack[_stackPtr++] = new DreamValue(Context.Strings[stringId]);
                                _stack[_stackPtr++] = new DreamValue(value);
                            }
                            break;
                        case Opcode.PushResource:
                            {
                                var pathId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                Push(new DreamValue(new DreamResource("resource", Context.Strings[pathId])));
                            }
                            break;
                        case Opcode.SwitchOnFloat:
                            {
                                var value = BinaryPrimitives.ReadSingleLittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var jumpAddress = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                var switchValue = _stack[_stackPtr - 1];
                                if (switchValue.Type == DreamValueType.Float && switchValue.RawFloat == value)
                                    pc = jumpAddress;
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.SwitchOnString:
                            {
                                var stringId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var jumpAddress = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                var switchValue = _stack[_stackPtr - 1];
                                if (switchValue.Type == DreamValueType.String && switchValue.TryGetValue(out string? s) && s == Context.Strings[stringId])
                                    pc = jumpAddress;
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.JumpIfReferenceFalse:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var address = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                var val = GetReferenceValue(reference, frame, 0);
                                PopCount(GetReferenceStackSize(reference));
                                if (val.IsFalse())
                                {
                                    pc = address;
                                }
                                else
                                {
                                    pc += 4;
                                }
                            }
                            break;
                        case Opcode.ReturnFloat:
                            {
                                var value = BinaryPrimitives.ReadSingleLittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                if (_stackPtr >= _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
                                _stack[_stackPtr++] = new DreamValue(value);
                                Opcode_Return(ref proc, ref pc);
                                potentiallyChangedStack = true;
                            }
                            break;
                        case Opcode.NPushFloatAssign:
                            {
                                int n = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc)); pc += 4;
                                float value = BinaryPrimitives.ReadSingleLittleEndian(bytecode.Slice(pc)); pc += 4;
                                var dv = new DreamValue(value);
                                for (int i = 0; i < n; i++)
                                {
                                    var reference = ReadReference(bytecode, ref pc);
                                    SetReferenceValue(reference, frame, dv, 0);
                                    PopCount(GetReferenceStackSize(reference));
                                }
                            }
                            break;
                        case Opcode.IsTypeDirect:
                            {
                                int typeId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc)); pc += 4;
                                var value = _stack[--_stackPtr];
                                bool result = false;
                                if (value.Type == DreamValueType.DreamObject && value.TryGetValue(out DreamObject? obj) && obj != null)
                                {
                                    var ot = obj.ObjectType;
                                    if (ot != null)
                                    {
                                        var targetType = Context.ObjectTypeManager?.GetObjectType(typeId);
                                        if (targetType != null)
                                            result = ot.IsSubtypeOf(targetType);
                                    }
                                }
                                _stack[_stackPtr++] = result ? DreamValue.True : DreamValue.False;
                            }
                            break;
                        case Opcode.NullRef:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                SetReferenceValue(reference, frame, DreamValue.Null, 0);
                                PopCount(GetReferenceStackSize(reference));
                            }
                            break;
                        case Opcode.IndexRefWithString:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var stringId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc)); pc += 4;
                                var stringValue = new DreamValue(Context.Strings[stringId]);

                                var objValue = GetReferenceValue(reference, frame, 0);
                                PopCount(GetReferenceStackSize(reference));
                                DreamValue result = DreamValue.Null;
                                if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
                                {
                                    result = list.GetValue(stringValue);
                                }

                                Push(result);
                            }
                            break;
                        case Opcode.ReturnReferenceValue:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var val = GetReferenceValue(reference, frame, 0);
                                PopCount(GetReferenceStackSize(reference));
                                Push(val);
                                Opcode_Return(ref proc, ref pc);
                                potentiallyChangedStack = true;
                            }
                            break;
                        case Opcode.PushFloatAssign:
                            {
                                var value = BinaryPrimitives.ReadSingleLittleEndian(bytecode.Slice(pc)); pc += 4;
                                var reference = ReadReference(bytecode, ref pc);
                                var dv = new DreamValue(value);
                                SetReferenceValue(reference, frame, dv, 0);
                                PopCount(GetReferenceStackSize(reference));
                                Push(dv);
                            }
                            break;

                        default:
                            throw new ScriptRuntimeException($"Unknown opcode: 0x{(byte)opcode:X2}", proc, pc - 1, CallStack);
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
                catch (Exception e) when (e is not ScriptRuntimeException)
                {
                    if (!HandleException(new ScriptRuntimeException("Unexpected internal error", proc, pc, CallStack, e)))
                        break;

                    // Resume execution if handled
                    frame = CallStack.Peek();
                    proc = frame.Proc;
                    pc = frame.PC;
                    bytecode = proc.Bytecode;
                }
                catch (ScriptRuntimeException e)
                {
                    if (!HandleException(e))
                        break;

                    // Resume execution if handled
                    frame = CallStack.Peek();
                    proc = frame.Proc;
                    pc = frame.PC;
                    bytecode = proc.Bytecode;
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
