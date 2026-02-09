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
    public partial class DreamThread : IScriptThread, IDisposable
    {
        private const int MaxCallStackDepth = 512;
        private const int MaxStackSize = 65536;
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

            PushCallFrame(new CallFrame(proc, 0, 0, associatedObject as DreamObject));
        }

        public DreamThread(DreamThread other, int pc)
        {
            Context = other.Context;
            _maxInstructions = other._maxInstructions;
            AssociatedObject = other.AssociatedObject;

            var currentFrame = other.CallStack.Peek();
            PushCallFrame(new CallFrame(currentFrame.Proc, pc, 0, currentFrame.Instance));
        }

        public void Sleep(float seconds)
        {
            State = DreamThreadState.Sleeping;
            SleepUntil = DateTime.Now.AddSeconds(seconds);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(DreamValue value)
        {
            if (_stackPtr >= MaxStackSize) throw new ScriptRuntimeException("Stack overflow", CurrentProc, CallStack.Peek().PC, CallStack);
            if (_stackPtr >= _stack.Length)
            {
                Array.Resize(ref _stack, _stack.Length * 2);
            }
            _stack[_stackPtr++] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DreamValue Pop()
        {
            if (_stackPtr <= 0) throw new ScriptRuntimeException("Stack underflow during Pop", CurrentProc, CallStack.Peek().PC, CallStack);
            return _stack[--_stackPtr];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        private void PushCallFrame(CallFrame frame)
        {
            if (CallStack.Count >= MaxCallStackDepth)
                throw new ScriptRuntimeException("Max call stack depth exceeded", frame.Proc, frame.PC, CallStack);
            CallStack.Push(frame);
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
                PushCallFrame(frame);

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
        public DMReference ReadReference(ReadOnlySpan<byte> bytecode, ref int pc)
        {
            var refType = (DMReference.Type)bytecode[pc++];
            switch (refType)
            {
                case DMReference.Type.Argument:
                case DMReference.Type.Local:
                    return new DMReference { RefType = refType, Index = bytecode[pc++] };
                case DMReference.Type.Global:
                case DMReference.Type.GlobalProc:
                    {
                        var globalIdx = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                        pc += 4;
                        return new DMReference { RefType = refType, Index = globalIdx };
                    }
                case DMReference.Type.Field:
                case DMReference.Type.SrcProc:
                case DMReference.Type.SrcField:
                    {
                        var nameId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                        pc += 4;
                        return new DMReference { RefType = refType, Name = Context.Strings[nameId] };
                    }
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
        public DreamValue GetReferenceValue(DMReference reference, CallFrame frame, int stackOffset = 0)
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
                    if (reference.Index < 0 || reference.Index >= frame.Proc.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", frame.Proc, 0, CallStack);
                    return _stack[frame.StackBase + reference.Index];
                case DMReference.Type.Local:
                    if (reference.Index < 0 || reference.Index >= frame.Proc.LocalVariableCount) throw new ScriptRuntimeException("Local index out of bounds", frame.Proc, 0, CallStack);
                    return _stack[frame.StackBase + frame.Proc.Arguments.Length + reference.Index];
                case DMReference.Type.SrcField:
                    {
                        if (frame.Instance == null) return DreamValue.Null;
                        int idx = frame.Instance.ObjectType?.GetVariableIndex(reference.Name) ?? -1;
                        return idx != -1 ? frame.Instance.GetVariableDirect(idx) : frame.Instance.GetVariable(reference.Name);
                    }
                case DMReference.Type.Field:
                    {
                        if (_stackPtr - 1 - stackOffset < 0) throw new ScriptRuntimeException("Stack underflow during Field reference access", frame.Proc, 0, CallStack);
                        var obj = _stack[_stackPtr - 1 - stackOffset];
                        if (obj.TryGetValue(out DreamObject? dreamObject) && dreamObject != null)
                        {
                            int idx = dreamObject.ObjectType?.GetVariableIndex(reference.Name) ?? -1;
                            return idx != -1 ? dreamObject.GetVariableDirect(idx) : dreamObject.GetVariable(reference.Name);
                        }
                        return DreamValue.Null;
                    }
                case DMReference.Type.ListIndex:
                    {
                        if (_stackPtr - 2 - stackOffset < 0) throw new ScriptRuntimeException("Stack underflow during ListIndex reference access", frame.Proc, 0, CallStack);
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
        public void SetReferenceValue(DMReference reference, CallFrame frame, DreamValue value, int stackOffset = 0)
        {
            switch (reference.RefType)
            {
                case DMReference.Type.Global:
                    Context.SetGlobal(reference.Index, value);
                    break;
                case DMReference.Type.Argument:
                    if (reference.Index < 0 || reference.Index >= frame.Proc.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", frame.Proc, 0, CallStack);
                    _stack[frame.StackBase + reference.Index] = value;
                    break;
                case DMReference.Type.Local:
                    if (reference.Index < 0 || reference.Index >= frame.Proc.LocalVariableCount) throw new ScriptRuntimeException("Local index out of bounds", frame.Proc, 0, CallStack);
                    _stack[frame.StackBase + frame.Proc.Arguments.Length + reference.Index] = value;
                    break;
                case DMReference.Type.SrcField:
                    if (frame.Instance != null)
                    {
                        int idx = frame.Instance.ObjectType?.GetVariableIndex(reference.Name) ?? -1;
                        if (idx != -1) frame.Instance.SetVariableDirect(idx, value);
                        else frame.Instance.SetVariable(reference.Name, value);
                    }
                    break;
                case DMReference.Type.Field:
                    {
                        if (_stackPtr - 1 - stackOffset < 0) throw new ScriptRuntimeException("Stack underflow during Field reference assignment", frame.Proc, 0, CallStack);
                        var obj = _stack[_stackPtr - 1 - stackOffset];
                        if (obj.TryGetValue(out DreamObject? dreamObject) && dreamObject != null)
                        {
                            int idx = dreamObject.ObjectType?.GetVariableIndex(reference.Name) ?? -1;
                            if (idx != -1) dreamObject.SetVariableDirect(idx, value);
                            else dreamObject.SetVariable(reference.Name, value);
                        }
                    }
                    break;
                case DMReference.Type.ListIndex:
                    {
                        if (_stackPtr - 2 - stackOffset < 0) throw new ScriptRuntimeException("Stack underflow during ListIndex reference assignment", frame.Proc, 0, CallStack);
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

            // Use local stack pointer for performance
            var stack = _stack;
            var stackPtr = _stackPtr;

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
                    _stackPtr = stackPtr;
                    Push(DreamValue.Null);
                    Opcode_Return(ref proc, ref pc);
                    stack = _stack;
                    stackPtr = _stackPtr;
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
                                if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(val); stack = _stack; stackPtr = _stackPtr; }
                                else stack[stackPtr++] = val;
                            }
                            break;
                        case Opcode.PushFloat:
                            {
                                var val = BinaryPrimitives.ReadSingleLittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var dv = new DreamValue(val);
                                if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(dv); stack = _stack; stackPtr = _stackPtr; }
                                else stack[stackPtr++] = dv;
                            }
                            break;
                        case Opcode.PushNull:
                            {
                                if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(DreamValue.Null); stack = _stack; stackPtr = _stackPtr; }
                                else stack[stackPtr++] = DreamValue.Null;
                            }
                            break;
                        case Opcode.Pop: stackPtr--; break;

                        case Opcode.Add:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Add", proc, pc, CallStack);
                                var b = stack[--stackPtr];
                                var a = stack[stackPtr - 1];
                                if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                                    stack[stackPtr - 1] = new DreamValue(a.RawFloat + b.RawFloat);
                                else
                                    stack[stackPtr - 1] = a + b;
                            }
                            break;
                        case Opcode.Subtract:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Subtract", proc, pc, CallStack);
                                var b = stack[--stackPtr];
                                var a = stack[stackPtr - 1];
                                if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                                    stack[stackPtr - 1] = new DreamValue(a.RawFloat - b.RawFloat);
                                else
                                    stack[stackPtr - 1] = a - b;
                            }
                            break;
                        case Opcode.Multiply:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Multiply", proc, pc, CallStack);
                                var b = stack[--stackPtr];
                                var a = stack[stackPtr - 1];
                                if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                                    stack[stackPtr - 1] = new DreamValue(a.RawFloat * b.RawFloat);
                                else
                                    stack[stackPtr - 1] = a * b;
                            }
                            break;
                        case Opcode.Divide:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Divide", proc, pc, CallStack);
                                var b = stack[--stackPtr];
                                var a = stack[stackPtr - 1];
                                if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                                {
                                    var fb = b.RawFloat;
                                    stack[stackPtr - 1] = new DreamValue(fb != 0 ? a.RawFloat / fb : 0);
                                }
                                else
                                    stack[stackPtr - 1] = a / b;
                            }
                            break;
                        case Opcode.CompareEquals:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareEquals", proc, pc, CallStack);
                                var b = stack[--stackPtr];
                                stack[stackPtr - 1] = stack[stackPtr - 1] == b ? DreamValue.True : DreamValue.False;
                            }
                            break;
                        case Opcode.CompareNotEquals:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareNotEquals", proc, pc, CallStack);
                                var b = stack[--stackPtr];
                                stack[stackPtr - 1] = stack[stackPtr - 1] != b ? DreamValue.True : DreamValue.False;
                            }
                            break;
                        case Opcode.CompareLessThan:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareLessThan", proc, pc, CallStack);
                                var b = stack[--stackPtr];
                                stack[stackPtr - 1] = stack[stackPtr - 1] < b ? DreamValue.True : DreamValue.False;
                            }
                            break;
                        case Opcode.CompareGreaterThan:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareGreaterThan", proc, pc, CallStack);
                                var b = stack[--stackPtr];
                                stack[stackPtr - 1] = stack[stackPtr - 1] > b ? DreamValue.True : DreamValue.False;
                            }
                            break;
                        case Opcode.CompareLessThanOrEqual:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareLessThanOrEqual", proc, pc, CallStack);
                                var b = stack[--stackPtr];
                                stack[stackPtr - 1] = stack[stackPtr - 1] <= b ? DreamValue.True : DreamValue.False;
                            }
                            break;
                        case Opcode.CompareGreaterThanOrEqual:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareGreaterThanOrEqual", proc, pc, CallStack);
                                var b = stack[--stackPtr];
                                stack[stackPtr - 1] = stack[stackPtr - 1] >= b ? DreamValue.True : DreamValue.False;
                            }
                            break;
                        case Opcode.CompareEquivalent:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareEquivalent", proc, pc, CallStack);
                                var b = stack[--stackPtr];
                                stack[stackPtr - 1] = stack[stackPtr - 1].Equals(b) ? DreamValue.True : DreamValue.False;
                            }
                            break;
                        case Opcode.CompareNotEquivalent:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareNotEquivalent", proc, pc, CallStack);
                                var b = stack[--stackPtr];
                                stack[stackPtr - 1] = !stack[stackPtr - 1].Equals(b) ? DreamValue.True : DreamValue.False;
                            }
                            break;
                        case Opcode.Negate:
                            {
                                if (stackPtr < 1) throw new ScriptRuntimeException("Stack underflow during Negate", proc, pc, CallStack);
                                stack[stackPtr - 1] = -stack[stackPtr - 1];
                            }
                            break;
                        case Opcode.BooleanNot:
                            {
                                if (stackPtr < 1) throw new ScriptRuntimeException("Stack underflow during BooleanNot", proc, pc, CallStack);
                                stack[stackPtr - 1] = stack[stackPtr - 1].IsFalse() ? DreamValue.True : DreamValue.False;
                            }
                            break;
                        case Opcode.Call:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var argType = (DMCallArgumentsType)bytecode[pc++];
                                var argStackDelta = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var unusedStackDelta = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;

                                SavePC(pc);
                                _stackPtr = stackPtr;
                                PerformCall(reference, argType, argStackDelta);
                                stack = _stack;
                                stackPtr = _stackPtr;
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
                                    _stackPtr = stackPtr;
                                    PerformCall(parentProc, instance, argStackDelta, argStackDelta);
                                    stack = _stack;
                                    stackPtr = _stackPtr;
                                }
                                else
                                {
                                    stackPtr -= argStackDelta;
                                    var val = DreamValue.Null;
                                    if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(val); stack = _stack; stackPtr = _stackPtr; }
                                    else stack[stackPtr++] = val;
                                }
                                potentiallyChangedStack = true;
                            }
                            break;
                        case Opcode.PushProc:
                            {
                                var procId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                DreamValue val;
                                if (procId >= 0 && procId < Context.AllProcs.Count)
                                    val = new DreamValue((IDreamProc)Context.AllProcs[procId]);
                                else
                                    val = DreamValue.Null;

                                if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(val); stack = _stack; stackPtr = _stackPtr; }
                                else stack[stackPtr++] = val;
                            }
                            break;
                        case Opcode.Jump:
                            {
                                pc = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                            }
                            break;
                        case Opcode.JumpIfFalse:
                            {
                                if (stackPtr < 1) throw new ScriptRuntimeException("Stack underflow during JumpIfFalse", proc, pc, CallStack);
                                var val = stack[--stackPtr];
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
                                _stackPtr = stackPtr;
                                var val = GetReferenceValue(reference, frame, 0);
                                PopCount(GetReferenceStackSize(reference));
                                stackPtr = _stackPtr;
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
                                _stackPtr = stackPtr;
                                var val = GetReferenceValue(reference, frame, 0);
                                PopCount(GetReferenceStackSize(reference));
                                stackPtr = _stackPtr;
                                if (val.IsFalse())
                                    pc = address;
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.Output:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Output", proc, pc, CallStack);
                                var message = stack[--stackPtr];
                                var target = stack[--stackPtr];

                                if (!message.IsNull)
                                {
                                    // TODO: Proper output routing based on target
                                    Console.WriteLine(message.ToString());
                                }
                            }
                            break;
                        case Opcode.OutputReference:
                            _stackPtr = stackPtr;
                            Opcode_OutputReference(proc, frame, ref pc);
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.Return:
                            _stackPtr = stackPtr;
                            Opcode_Return(ref proc, ref pc);
                            stack = _stack;
                            stackPtr = _stackPtr;
                            potentiallyChangedStack = true;
                            break;
                        case Opcode.BitAnd:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during BitAnd", proc, pc, CallStack);
                                var b = stack[--stackPtr];
                                stack[stackPtr - 1] &= b;
                            }
                            break;
                        case Opcode.BitOr:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during BitOr", proc, pc, CallStack);
                                var b = stack[--stackPtr];
                                stack[stackPtr - 1] |= b;
                            }
                            break;
                        case Opcode.BitXor:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during BitXor", proc, pc, CallStack);
                                var b = stack[--stackPtr];
                                stack[stackPtr - 1] ^= b;
                            }
                            break;
                        case Opcode.BitXorReference:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = stack[--stackPtr];
                                _stackPtr = stackPtr;
                                var refValue = GetReferenceValue(reference, frame, 0);
                                SetReferenceValue(reference, frame, refValue ^ value, 0);
                                PopCount(GetReferenceStackSize(reference));
                                stackPtr = _stackPtr;
                            }
                            break;
                        case Opcode.BitNot:
                            {
                                if (stackPtr < 1) throw new ScriptRuntimeException("Stack underflow during BitNot", proc, pc, CallStack);
                                stack[stackPtr - 1] = ~stack[stackPtr - 1];
                            }
                            break;
                        case Opcode.BitShiftLeft:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during BitShiftLeft", proc, pc, CallStack);
                                var b = stack[--stackPtr];
                                stack[stackPtr - 1] <<= b;
                            }
                            break;
                        case Opcode.BitShiftLeftReference:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = stack[--stackPtr];
                                _stackPtr = stackPtr;
                                var refValue = GetReferenceValue(reference, frame, 0);
                                SetReferenceValue(reference, frame, refValue << value, 0);
                                PopCount(GetReferenceStackSize(reference));
                                stackPtr = _stackPtr;
                            }
                            break;
                        case Opcode.BitShiftRight:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during BitShiftRight", proc, pc, CallStack);
                                var b = stack[--stackPtr];
                                stack[stackPtr - 1] >>= b;
                            }
                            break;
                        case Opcode.BitShiftRightReference:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = stack[--stackPtr];
                                _stackPtr = stackPtr;
                                var refValue = GetReferenceValue(reference, frame, 0);
                                SetReferenceValue(reference, frame, refValue >> value, 0);
                                PopCount(GetReferenceStackSize(reference));
                                stackPtr = _stackPtr;
                            }
                            break;

                        case Opcode.GetVariable:
                            {
                                var nameId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var instance = frame.Instance;
                                DreamValue val = DreamValue.Null;
                                if (instance != null)
                                {
                                    var name = Context.Strings[nameId];
                                    int idx = instance.ObjectType?.GetVariableIndex(name) ?? -1;
                                    val = idx != -1 ? instance.GetVariableDirect(idx) : instance.GetVariable(name);
                                }
                                if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(val); stack = _stack; stackPtr = _stackPtr; }
                                else stack[stackPtr++] = val;
                            }
                            break;
                        case Opcode.SetVariable:
                            {
                                var nameId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var val = stack[--stackPtr];
                                if (frame.Instance != null)
                                {
                                    var name = Context.Strings[nameId];
                                    int idx = frame.Instance.ObjectType?.GetVariableIndex(name) ?? -1;
                                    if (idx != -1) frame.Instance.SetVariableDirect(idx, val);
                                    else frame.Instance.SetVariable(name, val);
                                }
                            }
                            break;
                        case Opcode.PushReferenceValue:
                            {
                                var refType = (DMReference.Type)bytecode[pc++];
                                DreamValue val;
                                switch (refType)
                                {
                                    case DMReference.Type.Local:
                                        {
                                            int idx = bytecode[pc++];
                                            if (idx < 0 || idx >= frame.Proc.LocalVariableCount) throw new ScriptRuntimeException("Local index out of bounds", frame.Proc, pc, CallStack);
                                            val = stack[frame.StackBase + frame.Proc.Arguments.Length + idx];
                                        }
                                        break;
                                    case DMReference.Type.Argument:
                                        {
                                            int idx = bytecode[pc++];
                                            if (idx < 0 || idx >= frame.Proc.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", frame.Proc, pc, CallStack);
                                            val = stack[frame.StackBase + idx];
                                        }
                                        break;
                                    case DMReference.Type.Global:
                                        val = Context.GetGlobal(BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc)));
                                        pc += 4;
                                        break;
                                    case DMReference.Type.Src:
                                        val = frame.Instance != null ? new DreamValue(frame.Instance) : DreamValue.Null;
                                        break;
                                    default:
                                        {
                                            pc--;
                                            var reference = ReadReference(bytecode, ref pc);
                                            _stackPtr = stackPtr;
                                            val = GetReferenceValue(reference, frame, 0);
                                            PopCount(GetReferenceStackSize(reference));
                                            stackPtr = _stackPtr;
                                        }
                                        break;
                                }
                                if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(val); stack = _stack; stackPtr = _stackPtr; }
                                else stack[stackPtr++] = val;
                            }
                            break;
                        case Opcode.Assign:
                            {
                                var refType = (DMReference.Type)bytecode[pc++];
                                var value = stack[stackPtr - 1];
                                switch (refType)
                                {
                                    case DMReference.Type.Local:
                                        {
                                            int idx = bytecode[pc++];
                                            if (idx < 0 || idx >= frame.Proc.LocalVariableCount) throw new ScriptRuntimeException("Local index out of bounds", frame.Proc, pc, CallStack);
                                            stack[frame.StackBase + frame.Proc.Arguments.Length + idx] = value;
                                        }
                                        break;
                                    case DMReference.Type.Argument:
                                        {
                                            int idx = bytecode[pc++];
                                            if (idx < 0 || idx >= frame.Proc.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", frame.Proc, pc, CallStack);
                                            stack[frame.StackBase + idx] = value;
                                        }
                                        break;
                                    case DMReference.Type.Global:
                                        Context.SetGlobal(BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc)), value);
                                        pc += 4;
                                        break;
                                    default:
                                        pc--;
                                        var reference = ReadReference(bytecode, ref pc);
                                        _stackPtr = stackPtr;
                                        SetReferenceValue(reference, frame, value, 1);
                                        var val = Pop();
                                        PopCount(GetReferenceStackSize(reference));
                                        Push(val);
                                        stack = _stack;
                                        stackPtr = _stackPtr;
                                        break;
                                }
                            }
                            break;
                        case Opcode.PushGlobalVars:
                            _stackPtr = stackPtr;
                            Opcode_PushGlobalVars();
                            stack = _stack;
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.IsNull:
                            {
                                stack[stackPtr - 1] = stack[stackPtr - 1].IsNull ? DreamValue.True : DreamValue.False;
                            }
                            break;
                        case Opcode.JumpIfNull:
                            {
                                if (stackPtr < 1) throw new ScriptRuntimeException("Stack underflow during JumpIfNull", proc, pc, CallStack);
                                var val = stack[--stackPtr];
                                var address = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                if (val.Type == DreamValueType.Null)
                                    pc = address;
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.JumpIfNullNoPop:
                            {
                                var val = stack[stackPtr - 1];
                                var address = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                if (val.Type == DreamValueType.Null)
                                    pc = address;
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.SwitchCase:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during SwitchCase", proc, pc, CallStack);
                                var caseValue = stack[--stackPtr];
                                var switchValue = stack[stackPtr - 1];
                                var jumpAddress = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                if (switchValue == caseValue)
                                    pc = jumpAddress;
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.SwitchCaseRange:
                            {
                                if (stackPtr < 3) throw new ScriptRuntimeException("Stack underflow during SwitchCaseRange", proc, pc, CallStack);
                                var max = stack[--stackPtr];
                                var min = stack[--stackPtr];
                                var switchValue = stack[stackPtr - 1];
                                var jumpAddress = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                if (switchValue >= min && switchValue <= max)
                                    pc = jumpAddress;
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.BooleanAnd:
                            {
                                if (stackPtr < 1) throw new ScriptRuntimeException("Stack underflow during BooleanAnd", proc, pc, CallStack);
                                var val = stack[--stackPtr];
                                var jumpAddress = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                if (val.IsFalse())
                                {
                                    if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(val); stack = _stack; stackPtr = _stackPtr; }
                                    else stack[stackPtr++] = val;
                                    pc = jumpAddress;
                                }
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.BooleanOr:
                            {
                                if (stackPtr < 1) throw new ScriptRuntimeException("Stack underflow during BooleanOr", proc, pc, CallStack);
                                var val = stack[--stackPtr];
                                var jumpAddress = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                if (!val.IsFalse())
                                {
                                    if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(val); stack = _stack; stackPtr = _stackPtr; }
                                    else stack[stackPtr++] = val;
                                    pc = jumpAddress;
                                }
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.Increment:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                _stackPtr = stackPtr;
                                var value = GetReferenceValue(reference, frame, 0);
                                var newValue = value + 1;
                                SetReferenceValue(reference, frame, newValue, 0);
                                PopCount(GetReferenceStackSize(reference));
                                Push(newValue);
                                stack = _stack;
                                stackPtr = _stackPtr;
                            }
                            break;
                        case Opcode.Decrement:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                _stackPtr = stackPtr;
                                var value = GetReferenceValue(reference, frame, 0);
                                var newValue = value - 1;
                                SetReferenceValue(reference, frame, newValue, 0);
                                PopCount(GetReferenceStackSize(reference));
                                Push(newValue);
                                stack = _stack;
                                stackPtr = _stackPtr;
                            }
                            break;
                        case Opcode.Modulus:
                            _stackPtr = stackPtr;
                            Opcode_Modulus();
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.AssignInto:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = stack[--stackPtr];
                                _stackPtr = stackPtr;
                                SetReferenceValue(reference, frame, value, 0);
                                PopCount(GetReferenceStackSize(reference));
                                Push(value);
                                stack = _stack;
                                stackPtr = _stackPtr;
                            }
                            break;
                        case Opcode.ModulusReference:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = stack[--stackPtr];
                                _stackPtr = stackPtr;
                                var refValue = GetReferenceValue(reference, frame, 0);
                                SetReferenceValue(reference, frame, refValue % value, 0);
                                PopCount(GetReferenceStackSize(reference));
                                stackPtr = _stackPtr;
                            }
                            break;
                        case Opcode.ModulusModulus:
                            _stackPtr = stackPtr;
                            Opcode_ModulusModulus();
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.ModulusModulusReference:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = stack[--stackPtr];
                                _stackPtr = stackPtr;
                                var refValue = GetReferenceValue(reference, frame, 0);
                                SetReferenceValue(reference, frame, new DreamValue(SharedOperations.Modulo(refValue.GetValueAsFloat(), value.GetValueAsFloat())), 0);
                                PopCount(GetReferenceStackSize(reference));
                                stackPtr = _stackPtr;
                            }
                            break;
                        case Opcode.CreateList:
                            _stackPtr = stackPtr;
                            Opcode_CreateList(proc, ref pc);
                            stack = _stack;
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.CreateAssociativeList:
                            _stackPtr = stackPtr;
                            Opcode_CreateAssociativeList(proc, ref pc);
                            stack = _stack;
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.CreateStrictAssociativeList:
                            _stackPtr = stackPtr;
                            Opcode_CreateStrictAssociativeList(proc, ref pc);
                            stack = _stack;
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.IsInList:
                            _stackPtr = stackPtr;
                            Opcode_IsInList();
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.Input:
                            {
                                var ref1 = ReadReference(bytecode, ref pc);
                                var ref2 = ReadReference(bytecode, ref pc);
                                stackPtr -= 4; // user, message, title, default_value
                                var val = DreamValue.Null;
                                if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(val); stack = _stack; stackPtr = _stackPtr; }
                                else stack[stackPtr++] = val;
                            }
                            break;
                        case Opcode.PickUnweighted:
                            _stackPtr = stackPtr;
                            Opcode_PickUnweighted(proc, ref pc);
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.PickWeighted:
                            _stackPtr = stackPtr;
                            Opcode_PickWeighted(proc, ref pc);
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.DereferenceField:
                            {
                                if (stackPtr < 1) throw new ScriptRuntimeException("Stack underflow during DereferenceField", proc, pc, CallStack);
                                var nameId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var objValue = stack[--stackPtr];
                                DreamValue val = DreamValue.Null;
                                if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
                                {
                                    var name = Context.Strings[nameId];
                                    int idx = obj.ObjectType?.GetVariableIndex(name) ?? -1;
                                    val = idx != -1 ? obj.GetVariableDirect(idx) : obj.GetVariable(name);
                                }
                                if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(val); stack = _stack; stackPtr = _stackPtr; }
                                else stack[stackPtr++] = val;
                            }
                            break;
                        case Opcode.DereferenceIndex:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during DereferenceIndex", proc, pc, CallStack);
                                var index = stack[--stackPtr];
                                var objValue = stack[--stackPtr];
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
                                if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(val); stack = _stack; stackPtr = _stackPtr; }
                                else stack[stackPtr++] = val;
                            }
                            break;
                        case Opcode.PopReference:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                _stackPtr = stackPtr;
                                PopCount(GetReferenceStackSize(reference));
                                stackPtr = _stackPtr;
                            }
                            break;
                        case Opcode.DereferenceCall:
                            {
                                var nameId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var argType = (DMCallArgumentsType)bytecode[pc++];
                                var argStackDelta = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;

                                if (stackPtr < argStackDelta) throw new ScriptRuntimeException("Stack underflow during DereferenceCall", proc, pc, CallStack);
                                var objValue = stack[stackPtr - argStackDelta];
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
                                        int stackBase = stackPtr - argStackDelta;

                                        // Shift arguments down by 1, overwriting 'obj' on the stack
                                        for (int i = 0; i < argCount; i++)
                                        {
                                            stack[stackBase + i] = stack[stackBase + i + 1];
                                        }
                                        stackPtr--; // Effectively removed the object

                                        _stackPtr = stackPtr;
                                        PerformCall(targetProc, obj, argCount, argCount);
                                        stack = _stack;
                                        stackPtr = _stackPtr;
                                        potentiallyChangedStack = true;
                                        break;
                                    }
                                }

                                stackPtr -= argStackDelta;
                                var val = DreamValue.Null;
                                if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(val); stack = _stack; stackPtr = _stackPtr; }
                                else stack[stackPtr++] = val;
                                potentiallyChangedStack = true;
                            }
                            break;
                        case Opcode.Initial:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Initial", proc, pc, CallStack);
                                var key = stack[--stackPtr];
                                var objValue = stack[--stackPtr];
                                DreamValue result = DreamValue.Null;

                                if (objValue.TryGetValue(out DreamObject? obj) && obj != null && obj.ObjectType != null)
                                {
                                    if (key.TryGetValue(out string? varName) && varName != null)
                                    {
                                        int index = obj.ObjectType.GetVariableIndex(varName);
                                        if (index != -1 && index < obj.ObjectType.FlattenedDefaultValues.Count)
                                        {
                                            result = DreamValue.FromObject(obj.ObjectType.FlattenedDefaultValues[index]);
                                        }
                                    }
                                }
                                if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(result); stack = _stack; stackPtr = _stackPtr; }
                                else stack[stackPtr++] = result;
                            }
                            break;
                        case Opcode.IsType:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during IsType", proc, pc, CallStack);
                                var typeValue = stack[--stackPtr];
                                var objValue = stack[stackPtr - 1];
                                bool result = false;
                                if (objValue.Type == DreamValueType.DreamObject && typeValue.Type == DreamValueType.DreamType)
                                {
                                    var obj = objValue.GetValueAsDreamObject();
                                    typeValue.TryGetValue(out ObjectType? type);
                                    if (obj?.ObjectType != null && type != null)
                                        result = obj.ObjectType.IsSubtypeOf(type);
                                }
                                stack[stackPtr - 1] = result ? DreamValue.True : DreamValue.False;
                            }
                            break;
                        case Opcode.AsType:
                            {
                                if (stackPtr < 2) throw new ScriptRuntimeException("Stack underflow during AsType", proc, pc, CallStack);
                                var typeValue = stack[--stackPtr];
                                var objValue = stack[stackPtr - 1];
                                bool matches = false;
                                if (objValue.Type == DreamValueType.DreamObject && typeValue.Type == DreamValueType.DreamType)
                                {
                                    var obj = objValue.GetValueAsDreamObject();
                                    typeValue.TryGetValue(out ObjectType? type);
                                    if (obj?.ObjectType != null && type != null)
                                        matches = obj.ObjectType.IsSubtypeOf(type);
                                }
                                stack[stackPtr - 1] = matches ? objValue : DreamValue.Null;
                            }
                            break;
                        case Opcode.CreateListEnumerator:
                            _stackPtr = stackPtr;
                            Opcode_CreateListEnumerator(proc, ref pc);
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.Enumerate:
                            _stackPtr = stackPtr;
                            Opcode_Enumerate(proc, frame, ref pc);
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.EnumerateAssoc:
                            _stackPtr = stackPtr;
                            Opcode_EnumerateAssoc(proc, frame, ref pc);
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.DestroyEnumerator:
                            _stackPtr = stackPtr;
                            Opcode_DestroyEnumerator(proc, ref pc);
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.Append:
                            _stackPtr = stackPtr;
                            Opcode_Append(proc, frame, ref pc);
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.Remove:
                            _stackPtr = stackPtr;
                            Opcode_Remove(proc, frame, ref pc);
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.DeleteObject:
                            {
                                var value = stack[--stackPtr];
                                if (value.TryGetValueAsGameObject(out var obj))
                                {
                                    Context.GameState?.RemoveGameObject(obj);
                                }
                            }
                            break;
                        case Opcode.Prob:
                            _stackPtr = stackPtr;
                            Opcode_Prob();
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.IsSaved:
                            {
                                var val = DreamValue.True;
                                if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(val); stack = _stack; stackPtr = _stackPtr; }
                                else stack[stackPtr++] = val;
                            }
                            break;
                        case Opcode.GetStep:
                            _stackPtr = stackPtr;
                            Opcode_GetStep();
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.GetStepTo:
                            _stackPtr = stackPtr;
                            Opcode_GetStepTo();
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.GetDist:
                            _stackPtr = stackPtr;
                            Opcode_GetDist();
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.GetDir:
                            _stackPtr = stackPtr;
                            Opcode_GetDir();
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.MassConcatenation:
                            _stackPtr = stackPtr;
                            Opcode_MassConcatenation(proc, ref pc);
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.FormatString:
                            _stackPtr = stackPtr;
                            Opcode_FormatString(proc, ref pc);
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.Power:
                            _stackPtr = stackPtr;
                            Opcode_Power();
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.Sqrt:
                            _stackPtr = stackPtr;
                            Opcode_Sqrt();
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.Abs:
                            _stackPtr = stackPtr;
                            Opcode_Abs();
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.MultiplyReference:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = stack[--stackPtr];
                                _stackPtr = stackPtr;
                                var refValue = GetReferenceValue(reference, frame, 0);
                                SetReferenceValue(reference, frame, refValue * value, 0);
                                PopCount(GetReferenceStackSize(reference));
                                stackPtr = _stackPtr;
                            }
                            break;
                        case Opcode.Sin:
                            _stackPtr = stackPtr;
                            Opcode_Sin();
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.DivideReference:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = stack[--stackPtr];
                                _stackPtr = stackPtr;
                                var refValue = GetReferenceValue(reference, frame, 0);
                                SetReferenceValue(reference, frame, refValue / value, 0);
                                PopCount(GetReferenceStackSize(reference));
                                stackPtr = _stackPtr;
                            }
                            break;
                        case Opcode.Cos:
                            _stackPtr = stackPtr;
                            Opcode_Cos();
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.Tan:
                            _stackPtr = stackPtr;
                            Opcode_Tan();
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.ArcSin:
                            _stackPtr = stackPtr;
                            Opcode_ArcSin();
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.ArcCos:
                            _stackPtr = stackPtr;
                            Opcode_ArcCos();
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.ArcTan:
                            _stackPtr = stackPtr;
                            Opcode_ArcTan();
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.ArcTan2:
                            _stackPtr = stackPtr;
                            Opcode_ArcTan2();
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.Log:
                            {
                                var baseValue = stack[--stackPtr];
                                var x = stack[--stackPtr];
                                var val = new DreamValue(MathF.Log(x.GetValueAsFloat(), baseValue.GetValueAsFloat()));
                                if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(val); stack = _stack; stackPtr = _stackPtr; }
                                else stack[stackPtr++] = val;
                            }
                            break;
                        case Opcode.LogE:
                            {
                                var val = new DreamValue(MathF.Log(stack[--stackPtr].GetValueAsFloat()));
                                if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(val); stack = _stack; stackPtr = _stackPtr; }
                                else stack[stackPtr++] = val;
                            }
                            break;
                        case Opcode.PushType:
                            {
                                var typeId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var type = Context.ObjectTypeManager?.GetObjectType(typeId);
                                DreamValue val = type != null ? new DreamValue(type) : DreamValue.Null;
                                if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(val); stack = _stack; stackPtr = _stackPtr; }
                                else stack[stackPtr++] = val;
                            }
                            break;
                        case Opcode.CreateObject:
                            _stackPtr = stackPtr;
                            Opcode_CreateObject(proc, ref pc);
                            stack = _stack;
                            stackPtr = _stackPtr;
                            potentiallyChangedStack = true;
                            break;
                        case Opcode.LocateCoord:
                            _stackPtr = stackPtr;
                            Opcode_LocateCoord();
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.Locate:
                            _stackPtr = stackPtr;
                            Opcode_Locate();
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.Length:
                            {
                                var value = stack[--stackPtr];
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

                                if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(result); stack = _stack; stackPtr = _stackPtr; }
                                else stack[stackPtr++] = result;
                            }
                            break;
                        case Opcode.IsInRange:
                            {
                                if (stackPtr < 3) throw new ScriptRuntimeException("Stack underflow during IsInRange", proc, pc, CallStack);
                                var max = stack[--stackPtr];
                                var min = stack[--stackPtr];
                                var val = stack[--stackPtr];
                                var result = new DreamValue(val >= min && val <= max ? 1 : 0);
                                if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(result); stack = _stack; stackPtr = _stackPtr; }
                                else stack[stackPtr++] = result;
                            }
                            break;
                        case Opcode.Throw:
                            {
                                var value = stack[--stackPtr];
                                var e = new ScriptRuntimeException(value.ToString(), proc, pc, CallStack) { ThrownValue = value };
                                _stackPtr = stackPtr;
                                HandleException(e);
                                stack = _stack;
                                stackPtr = _stackPtr;
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
                                    StackPointer = stackPtr,
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
                                    StackPointer = stackPtr,
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
                                if (stackPtr < 1) throw new ScriptRuntimeException("Stack underflow during Spawn", proc, pc, CallStack);
                                var address = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                var bodyPc = pc + 4;
                                pc = address; // Skip body in main thread

                                var delay = stack[--stackPtr];

                                _stackPtr = stackPtr;
                                var newThread = new DreamThread(this, bodyPc);
                                if (delay.TryGetValue(out float seconds) && seconds > 0)
                                {
                                    newThread.Sleep(seconds / 10.0f);
                                }
                                Context.ScriptHost?.AddThread(newThread);
                                stackPtr = _stackPtr;
                            }
                            break;
                        case Opcode.Rgb:
                            _stackPtr = stackPtr;
                            Opcode_Rgb(proc, ref pc);
                            stackPtr = _stackPtr;
                            break;
                        case Opcode.Gradient:
                            _stackPtr = stackPtr;
                            Opcode_Gradient(proc, ref pc);
                            stackPtr = _stackPtr;
                            break;

                        // Optimized opcodes
                        case Opcode.AppendNoPush:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = stack[--stackPtr];
                                _stackPtr = stackPtr;
                                var listValue = GetReferenceValue(reference, frame);

                                if (listValue.Type == DreamValueType.DreamObject && listValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
                                {
                                    list.AddValue(value);
                                }
                                stackPtr = _stackPtr;
                            }
                            break;
                        case Opcode.AssignNoPush:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var value = stack[--stackPtr];
                                _stackPtr = stackPtr;
                                SetReferenceValue(reference, frame, value);
                                stackPtr = _stackPtr;
                            }
                            break;
                        case Opcode.PushRefAndDereferenceField:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var fieldNameId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var fieldName = Context.Strings[fieldNameId];

                                _stackPtr = stackPtr;
                                var objValue = GetReferenceValue(reference, frame);
                                DreamValue val = DreamValue.Null;
                                if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj != null)
                                {
                                    val = obj.GetVariable(fieldName);
                                }
                                stackPtr = _stackPtr;
                                if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(val); stack = _stack; stackPtr = _stackPtr; }
                                else stack[stackPtr++] = val;
                            }
                            break;
                        case Opcode.PushNRefs:
                            {
                                var count = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                _stackPtr = stackPtr;
                                for (int i = 0; i < count; i++)
                                {
                                    var reference = ReadReference(bytecode, ref pc);
                                    var val = GetReferenceValue(reference, frame);
                                    Push(val);
                                }
                                stack = _stack;
                                stackPtr = _stackPtr;
                            }
                            break;
                        case Opcode.PushNFloats:
                            {
                                var count = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                for (int i = 0; i < count; i++)
                                {
                                    var val = new DreamValue(BinaryPrimitives.ReadSingleLittleEndian(bytecode.Slice(pc)));
                                    pc += 4;
                                    if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(val); stack = _stack; stackPtr = _stackPtr; }
                                    else stack[stackPtr++] = val;
                                }
                            }
                            break;
                        case Opcode.PushStringFloat:
                            {
                                var stringId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var value = BinaryPrimitives.ReadSingleLittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var val1 = new DreamValue(Context.Strings[stringId]);
                                var val2 = new DreamValue(value);

                                if (stackPtr + 1 >= stack.Length) { _stackPtr = stackPtr; Push(val1); Push(val2); stack = _stack; stackPtr = _stackPtr; }
                                else { stack[stackPtr++] = val1; stack[stackPtr++] = val2; }
                            }
                            break;
                        case Opcode.PushResource:
                            {
                                var pathId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var val = new DreamValue(new DreamResource("resource", Context.Strings[pathId]));
                                if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(val); stack = _stack; stackPtr = _stackPtr; }
                                else stack[stackPtr++] = val;
                            }
                            break;
                        case Opcode.SwitchOnFloat:
                            {
                                if (stackPtr < 1) throw new ScriptRuntimeException("Stack underflow during SwitchOnFloat", proc, pc, CallStack);
                                var value = BinaryPrimitives.ReadSingleLittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var jumpAddress = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                var switchValue = stack[stackPtr - 1];
                                if (switchValue.Type == DreamValueType.Float && switchValue.RawFloat == value)
                                    pc = jumpAddress;
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.SwitchOnString:
                            {
                                if (stackPtr < 1) throw new ScriptRuntimeException("Stack underflow during SwitchOnString", proc, pc, CallStack);
                                var stringId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var jumpAddress = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
                                var switchValue = stack[stackPtr - 1];
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
                                _stackPtr = stackPtr;
                                var val = GetReferenceValue(reference, frame, 0);
                                PopCount(GetReferenceStackSize(reference));
                                stackPtr = _stackPtr;
                                if (val.IsFalse())
                                    pc = address;
                                else
                                    pc += 4;
                            }
                            break;
                        case Opcode.ReturnFloat:
                            {
                                var value = BinaryPrimitives.ReadSingleLittleEndian(bytecode.Slice(pc));
                                pc += 4;
                                var val = new DreamValue(value);
                                if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(val); stack = _stack; stackPtr = _stackPtr; }
                                else stack[stackPtr++] = val;

                                _stackPtr = stackPtr;
                                Opcode_Return(ref proc, ref pc);
                                stack = _stack;
                                stackPtr = _stackPtr;
                                potentiallyChangedStack = true;
                            }
                            break;
                        case Opcode.NPushFloatAssign:
                            {
                                int n = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc)); pc += 4;
                                float value = BinaryPrimitives.ReadSingleLittleEndian(bytecode.Slice(pc)); pc += 4;
                                var dv = new DreamValue(value);
                                _stackPtr = stackPtr;
                                for (int i = 0; i < n; i++)
                                {
                                    var reference = ReadReference(bytecode, ref pc);
                                    SetReferenceValue(reference, frame, dv, 0);
                                    PopCount(GetReferenceStackSize(reference));
                                }
                                stackPtr = _stackPtr;
                            }
                            break;
                        case Opcode.IsTypeDirect:
                            {
                                int typeId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc)); pc += 4;
                                var value = stack[--stackPtr];
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
                                if (stackPtr >= stack.Length) { _stackPtr = stackPtr; Push(result ? DreamValue.True : DreamValue.False); stack = _stack; stackPtr = _stackPtr; }
                                else stack[stackPtr++] = result ? DreamValue.True : DreamValue.False;
                            }
                            break;
                        case Opcode.NullRef:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                _stackPtr = stackPtr;
                                SetReferenceValue(reference, frame, DreamValue.Null, 0);
                                PopCount(GetReferenceStackSize(reference));
                                stackPtr = _stackPtr;
                            }
                            break;
                        case Opcode.IndexRefWithString:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                var stringId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc)); pc += 4;
                                var stringValue = new DreamValue(Context.Strings[stringId]);

                                _stackPtr = stackPtr;
                                var objValue = GetReferenceValue(reference, frame, 0);
                                PopCount(GetReferenceStackSize(reference));
                                DreamValue result = DreamValue.Null;
                                if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
                                {
                                    result = list.GetValue(stringValue);
                                }

                                Push(result);
                                stack = _stack;
                                stackPtr = _stackPtr;
                            }
                            break;
                        case Opcode.ReturnReferenceValue:
                            {
                                var reference = ReadReference(bytecode, ref pc);
                                _stackPtr = stackPtr;
                                var val = GetReferenceValue(reference, frame, 0);
                                PopCount(GetReferenceStackSize(reference));
                                Push(val);
                                Opcode_Return(ref proc, ref pc);
                                stack = _stack;
                                stackPtr = _stackPtr;
                                potentiallyChangedStack = true;
                            }
                            break;
                        case Opcode.PushFloatAssign:
                            {
                                var value = BinaryPrimitives.ReadSingleLittleEndian(bytecode.Slice(pc)); pc += 4;
                                var reference = ReadReference(bytecode, ref pc);
                                var dv = new DreamValue(value);
                                _stackPtr = stackPtr;
                                SetReferenceValue(reference, frame, dv, 0);
                                PopCount(GetReferenceStackSize(reference));
                                Push(dv);
                                stack = _stack;
                                stackPtr = _stackPtr;
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
                    _stackPtr = stackPtr;
                    if (!HandleException(new ScriptRuntimeException("Unexpected internal error", proc, pc, CallStack, e)))
                        break;

                    // Resume execution if handled
                    frame = CallStack.Peek();
                    proc = frame.Proc;
                    pc = frame.PC;
                    bytecode = proc.Bytecode;
                    stackPtr = _stackPtr;
                }
                catch (ScriptRuntimeException e)
                {
                    _stackPtr = stackPtr;
                    if (!HandleException(e))
                        break;

                    // Resume execution if handled
                    frame = CallStack.Peek();
                    proc = frame.Proc;
                    pc = frame.PC;
                    bytecode = proc.Bytecode;
                    stackPtr = _stackPtr;
                }
            }

            if (CallStack.Count > 0)
            {
                SavePC(pc);
            }

            _stackPtr = stackPtr;
            return State;
        }

        public void Dispose()
        {
            foreach (var enumerator in ActiveEnumerators.Values)
            {
                enumerator.Dispose();
            }
            ActiveEnumerators.Clear();
            EnumeratorLists.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
