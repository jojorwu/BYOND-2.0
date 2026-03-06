using Shared.Enums;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Core.VM.Procs;
using Shared;

namespace Core.VM.Runtime;

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
    public const int MaxCallStackDepth = 65536;
    public const int MaxStackSize = 10485760;
    internal DreamValue[] _stack;
    internal int _stackPtr = 0;
    internal CallFrame[] _callStack = new CallFrame[1024];
    internal int _callStackPtr = 0;
    private TryBlock[] _tryStack = ArrayPool<TryBlock>.Shared.Rent(1024);
    private int _tryStackPtr = 0;

    internal void PushTryBlock(TryBlock tryBlock)
    {
        if (_tryStackPtr >= _tryStack.Length)
        {
            var newStack = ArrayPool<TryBlock>.Shared.Rent(_tryStack.Length * 2);
            Array.Copy(_tryStack, newStack, _tryStack.Length);
            ArrayPool<TryBlock>.Shared.Return(_tryStack);
            _tryStack = newStack;
        }
        _tryStack[_tryStackPtr++] = tryBlock;
    }

    internal void PopTryBlock()
    {
        if (_tryStackPtr > 0) _tryStackPtr--;
    }

    private readonly IEnumerator<DreamValue>?[] _activeEnumeratorsArray = new IEnumerator<DreamValue>[1024];
    private readonly DreamList?[] _enumeratorListsArray = new DreamList[1024];
    public readonly Dictionary<int, IEnumerator<DreamValue>> ActiveEnumerators = new();
    public readonly Dictionary<int, DreamList> EnumeratorLists = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerator<DreamValue>? GetEnumerator(int id)
    {
        if (id >= 0 && id < 1024) return _activeEnumeratorsArray[id];
        return ActiveEnumerators.TryGetValue(id, out var enumerator) ? enumerator : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetEnumerator(int id, IEnumerator<DreamValue> enumerator, DreamList? list)
    {
        if (id >= 0 && id < 1024)
        {
            _activeEnumeratorsArray[id] = enumerator;
            _enumeratorListsArray[id] = list;
        }
        else
        {
            ActiveEnumerators[id] = enumerator;
            if (list != null) EnumeratorLists[id] = list;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DreamList? GetEnumeratorList(int id)
    {
        if (id >= 0 && id < 1024) return _enumeratorListsArray[id];
        return EnumeratorLists.TryGetValue(id, out var list) ? list : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveEnumerator(int id)
    {
        if (id >= 0 && id < 1024)
        {
            _activeEnumeratorsArray[id] = null;
            _enumeratorListsArray[id] = null;
        }
        else
        {
            ActiveEnumerators.Remove(id);
            EnumeratorLists.Remove(id);
        }
    }
    public int StackCount => _stackPtr;
    public int CallStackCount => _callStackPtr;

    public IEnumerable<CallFrame> CallStack => _callStack.Take(_callStackPtr).Reverse();

    public DreamProc CurrentProc => _callStack[_callStackPtr - 1].Proc;
    private volatile int _state = (int)DreamThreadState.Running;
    public DreamThreadState State { get => (DreamThreadState)_state; internal set => _state = (int)value; }
    public DateTime SleepUntil { get; internal set; }
    public IGameObject? AssociatedObject { get; }
    public DreamObject? Usr { get; set; }

    public ScriptThreadPriority Priority { get; set; } = ScriptThreadPriority.Normal;
    public TimeSpan ExecutionTime { get; set; } = TimeSpan.Zero;
    public int WaitTicks { get; set; } = 0;
    public long TotalInstructionsExecuted => _totalInstructionsExecuted;
    public int InstructionQuotaBalance { get; set; } = 0;

    public DreamVMContext Context { get; }
    internal readonly int _maxInstructions;
    internal long _totalInstructionsExecuted;
    private readonly IBytecodeInterpreter _interpreter;

    public DreamThread(DreamProc proc, DreamVMContext context, int maxInstructions, IGameObject? associatedObject = null, IBytecodeInterpreter? interpreter = null)
    {
        Context = context;
        _maxInstructions = maxInstructions;
        _interpreter = interpreter ?? new BytecodeInterpreter();
        AssociatedObject = associatedObject;
        _stack = ArrayPool<DreamValue>.Shared.Rent(Math.Max(1024, proc.LocalVariableCount));

        PushCallFrame(new CallFrame(proc, 0, 0, associatedObject as DreamObject));

        // Initialize locals for the entry-point frame
        int localCount = proc.LocalVariableCount;
        if (localCount > 0)
        {
            _stack.AsSpan(0, localCount).Fill(DreamValue.Null);
            _stackPtr = localCount;
        }
    }

    public DreamThread(DreamThread other, int pc)
    {
        Context = other.Context;
        _maxInstructions = other._maxInstructions;
        _interpreter = other._interpreter;
        AssociatedObject = other.AssociatedObject;

        // Rent and copy stack
        _stack = ArrayPool<DreamValue>.Shared.Rent(other._stack.Length);
        Array.Copy(other._stack, _stack, other._stackPtr);
        _stackPtr = other._stackPtr;

        var currentFrame = other._callStack[other._callStackPtr - 1];
        PushCallFrame(new CallFrame(currentFrame.Proc, pc, 0, currentFrame.Instance));
    }

    public void Sleep(double seconds)
    {
        if (double.IsNaN(seconds) || seconds <= 0) seconds = 0;
        // Max 1 year to prevent DateTime overflow
        if (seconds > 31536000) seconds = 31536000;

        State = DreamThreadState.Sleeping;
        SleepUntil = DateTime.Now.AddSeconds(seconds);
    }

    public void WakeUp()
    {
        if (State == DreamThreadState.Sleeping)
        {
            State = DreamThreadState.Running;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(DreamValue value)
    {
        if ((uint)_stackPtr >= (uint)MaxStackSize) throw new ScriptRuntimeException("Stack overflow", CurrentProc, (_callStackPtr > 0 ? _callStack[_callStackPtr - 1] : default).PC, this);
        if (_stackPtr >= _stack.Length)
        {
            var newStack = ArrayPool<DreamValue>.Shared.Rent(_stack.Length * 2);
            Array.Copy(_stack, newStack, _stackPtr);
            ArrayPool<DreamValue>.Shared.Return(_stack, true);
            _stack = newStack;
        }
        _stack[_stackPtr++] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureStackCapacity(int count)
    {
        if (_stackPtr + count >= _stack.Length)
        {
            int newSize = Math.Min(MaxStackSize, Math.Max(_stack.Length * 2, _stackPtr + count + 1024));
            if (newSize <= _stack.Length) return; // Cannot expand further within MaxStackSize

            var newStack = ArrayPool<DreamValue>.Shared.Rent(newSize);
            Array.Copy(_stack, newStack, _stackPtr);
            ArrayPool<DreamValue>.Shared.Return(_stack, true);
            _stack = newStack;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DreamValue Pop()
    {
        if (_stackPtr <= 0) throw new ScriptRuntimeException("Stack underflow during Pop", CurrentProc, (_callStackPtr > 0 ? _callStack[_callStackPtr - 1] : default).PC, this);
        return _stack[--_stackPtr];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DreamValue Peek()
    {
        if (_stackPtr <= 0) throw new ScriptRuntimeException("Stack underflow during Peek", CurrentProc, (_callStackPtr > 0 ? _callStack[_callStackPtr - 1] : default).PC, this);
        return _stack[_stackPtr - 1];
    }

    public DreamValue Peek(int offset)
    {
        if (_stackPtr - offset - 1 < 0) throw new ScriptRuntimeException($"Stack underflow during Peek({offset})", CurrentProc, (_callStackPtr > 0 ? _callStack[_callStackPtr - 1] : default).PC, this);
        return _stack[_stackPtr - offset - 1];
    }

    public void PopCount(int count)
    {
        if (_stackPtr < count) throw new ScriptRuntimeException($"Stack underflow during PopCount({count})", CurrentProc, (_callStackPtr > 0 ? _callStack[_callStackPtr - 1] : default).PC, this);
        // Console.WriteLine($"PopCount: {count}, OldPtr: {_stackPtr}, NewPtr: {_stackPtr - count}");
        _stackPtr -= count;
    }

    internal byte ReadByte(DreamProc proc, ref int pc)
    {
        if (pc + 1 > proc.Bytecode.Length)
            throw new Exception("Attempted to read past the end of the bytecode.");
        return proc.Bytecode[pc++];
    }

    internal int ReadInt32(DreamProc proc, ref int pc)
    {
        if (pc + 4 > proc.Bytecode.Length)
            throw new Exception("Attempted to read past the end of the bytecode.");
        var value = BinaryPrimitives.ReadInt32LittleEndian(proc.Bytecode.AsSpan(pc));
        pc += 4;
        return value;
    }

    internal double ReadDouble(DreamProc proc, ref int pc)
    {
        if (pc + 8 > proc.Bytecode.Length)
            throw new Exception("Attempted to read past the end of the bytecode.");
        var value = BinaryPrimitives.ReadDoubleLittleEndian(proc.Bytecode.AsSpan(pc));
        pc += 8;
        return value;
    }

    internal void SavePC(int pc)
    {
        if (_callStackPtr > 0)
        {
            _callStack[_callStackPtr - 1].PC = pc;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PushCallFrame(CallFrame frame)
    {
        if ((uint)_callStackPtr >= (uint)MaxCallStackDepth)
            throw new ScriptRuntimeException("Max call stack depth exceeded", frame.Proc, frame.PC, this);

        if (_callStackPtr >= _callStack.Length)
        {
            Array.Resize(ref _callStack, _callStack.Length * 2);
        }
        _callStack[_callStackPtr++] = frame;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CallFrame PopCallFrame()
    {
        if (_callStackPtr <= 0) throw new Exception("Call stack underflow");
        var frame = _callStack[--_callStackPtr];
        _callStack[_callStackPtr] = default; // Clear reference
        return frame;
    }

    internal bool HandleException(ScriptRuntimeException e)
    {
        if (_tryStackPtr > 0)
        {
            var tryBlock = _tryStack[--_tryStackPtr];

            // Unwind CallStack
            Array.Clear(_callStack, tryBlock.CallStackDepth, _callStackPtr - tryBlock.CallStackDepth);
            _callStackPtr = tryBlock.CallStackDepth;

            // Restore stack pointer
            _stackPtr = tryBlock.StackPointer;

            // Set catch variable if needed
            if (tryBlock.CatchReference.HasValue)
            {
                var catchValue = e.ThrownValue ?? new DreamValue(e.Message);
                SetReferenceValue(tryBlock.CatchReference.Value, ref _callStack[_callStackPtr - 1], catchValue, 0);
                PopCount(GetReferenceStackSize(tryBlock.CatchReference.Value));
            }

            // Jump to catch address
            _callStack[_callStackPtr - 1].PC = tryBlock.CatchAddress;

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
        if (refType == DMReference.Type.Local || refType == DMReference.Type.Argument)
        {
            var idx = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
            pc += 4;
            return new DMReference { RefType = refType, Index = idx };
        }

        if (refType >= DMReference.Type.Global && refType <= DMReference.Type.GlobalProc)
        {
            var globalIdx = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
            pc += 4;
            return new DMReference { RefType = refType, Index = globalIdx };
        }

        if (refType >= DMReference.Type.Field && refType <= DMReference.Type.SrcField)
        {
            var nameId = BinaryPrimitives.ReadInt32LittleEndian(bytecode.Slice(pc));
            pc += 4;
            return new DMReference { RefType = refType, Name = Context.Strings[nameId] };
        }

        return new DMReference { RefType = refType };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetReferenceStackSize(DMReference reference)
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
    public DreamValue GetReferenceValue(DMReference reference, ref CallFrame frame, int stackOffset = 0)
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
                    if (frame.ArgsList != null) return new DreamValue(frame.ArgsList);
                    var list = new DreamList(Context.ListType);
                    for (int i = 0; i < frame.Proc.Arguments.Length; i++)
                    {
                        list.AddValue(_stack[frame.StackBase + i]);
                    }
                    frame.ArgsList = list;
                    _callStack[_callStackPtr - 1].ArgsList = list;
                    return new DreamValue(list);
                }
            case DMReference.Type.Global:
                return Context.GetGlobal(reference.Index);
            case DMReference.Type.Argument:
                if (reference.Index < 0 || reference.Index >= frame.Proc.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", frame.Proc, 0, this);
                return _stack[frame.ArgumentBase + reference.Index];
            case DMReference.Type.Local:
                if (reference.Index < 0 || reference.Index >= frame.Proc.LocalVariableCount) throw new ScriptRuntimeException("Local index out of bounds", frame.Proc, 0, this);
                return _stack[frame.LocalBase + reference.Index];
            case DMReference.Type.SrcField:
                {
                    if (frame.Instance == null) return DreamValue.Null;
                    int idx = frame.Instance.ObjectType?.GetVariableIndex(reference.Name) ?? -1;
                    return idx != -1 ? frame.Instance.GetVariableDirect(idx) : frame.Instance.GetVariable(reference.Name);
                }
            case DMReference.Type.Field:
                {
                    if (_stackPtr - 1 - stackOffset < 0) throw new ScriptRuntimeException("Stack underflow during Field reference access", frame.Proc, 0, this);
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
                    if (_stackPtr - 2 - stackOffset < 0) throw new ScriptRuntimeException("Stack underflow during ListIndex reference access", frame.Proc, 0, this);
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
    public void SetReferenceValue(DMReference reference, ref CallFrame frame, DreamValue value, int stackOffset = 0)
    {
        switch (reference.RefType)
        {
            case DMReference.Type.Global:
                Context.SetGlobal(reference.Index, value);
                break;
            case DMReference.Type.Argument:
                if (reference.Index < 0 || reference.Index >= frame.Proc.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", frame.Proc, 0, this);
                _stack[frame.ArgumentBase + reference.Index] = value;
                break;
            case DMReference.Type.Local:
                if (reference.Index < 0 || reference.Index >= frame.Proc.LocalVariableCount) throw new ScriptRuntimeException("Local index out of bounds", frame.Proc, 0, this);
                _stack[frame.LocalBase + reference.Index] = value;
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
                    if (_stackPtr - 1 - stackOffset < 0) throw new ScriptRuntimeException("Stack underflow during Field reference assignment", frame.Proc, 0, this);
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
                    if (_stackPtr - 2 - stackOffset < 0) throw new ScriptRuntimeException("Stack underflow during ListIndex reference assignment", frame.Proc, 0, this);
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

    public DreamThreadState Run(int instructionBudget)
    {
        return _interpreter.Run(this, instructionBudget);
    }

    public void Dispose()
    {
        if (_tryStack != null)
        {
            ArrayPool<TryBlock>.Shared.Return(_tryStack);
            _tryStack = null!;
        }

        foreach (var enumerator in ActiveEnumerators.Values)
        {
            enumerator.Dispose();
        }
        ActiveEnumerators.Clear();
        EnumeratorLists.Clear();
        Usr = null;

        // Clear call stack references
        Array.Clear(_callStack, 0, _callStackPtr);
        _callStackPtr = 0;

        if (_stack != null)
        {
            ArrayPool<DreamValue>.Shared.Return(_stack, true);
            _stack = null!;
        }

        GC.SuppressFinalize(this);
    }

    ~DreamThread()
    {
        Dispose();
    }
}
