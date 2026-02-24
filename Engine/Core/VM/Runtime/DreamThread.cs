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
    public const int MaxCallStackDepth = 512;
    public const int MaxStackSize = 65536;
    internal DreamValue[] _stack;
    internal int _stackPtr = 0;
    internal CallFrame[] _callStack = new CallFrame[64];
    internal int _callStackPtr = 0;
    public Stack<TryBlock> TryStack { get; } = new();
    public Dictionary<int, IEnumerator<DreamValue>> ActiveEnumerators { get; } = new();
    public Dictionary<int, DreamList> EnumeratorLists { get; } = new();
    public int StackCount => _stackPtr;
    public int CallStackCount => _callStackPtr;

    public IEnumerable<CallFrame> CallStack => _callStack.Take(_callStackPtr).Reverse();

    public DreamProc CurrentProc => _callStack[_callStackPtr - 1].Proc;
    public DreamThreadState State { get; internal set; } = DreamThreadState.Running;
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
        _stack = ArrayPool<DreamValue>.Shared.Rent(1024);

        PushCallFrame(new CallFrame(proc, 0, 0, associatedObject as DreamObject));
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

    public void Sleep(float seconds)
    {
        if (float.IsNaN(seconds) || seconds <= 0) seconds = 0;
        // Max 24 hours to prevent DateTime overflow
        if (seconds > 86400) seconds = 86400;

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
        if (_stackPtr >= MaxStackSize) throw new ScriptRuntimeException("Stack overflow", CurrentProc, (_callStackPtr > 0 ? _callStack[_callStackPtr - 1] : default).PC, this);
        if (_stackPtr >= _stack.Length)
        {
            var newStack = ArrayPool<DreamValue>.Shared.Rent(_stack.Length * 2);
            Array.Copy(_stack, newStack, _stack.Length);
            ArrayPool<DreamValue>.Shared.Return(_stack, true);
            _stack = newStack;
        }
        _stack[_stackPtr++] = value;
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

    internal float ReadSingle(DreamProc proc, ref int pc)
    {
        if (pc + 4 > proc.Bytecode.Length)
            throw new Exception("Attempted to read past the end of the bytecode.");
        var value = BinaryPrimitives.ReadSingleLittleEndian(proc.Bytecode.AsSpan(pc));
        pc += 4;
        return value;
    }

    internal void SavePC(int pc)
    {
        if (_callStackPtr > 0)
        {
            _callStack[_callStackPtr - 1].PC = pc;
        }
    }

    public void PushCallFrame(CallFrame frame)
    {
        if (_callStackPtr >= MaxCallStackDepth)
            throw new ScriptRuntimeException("Max call stack depth exceeded", frame.Proc, frame.PC, this);

        if (_callStackPtr >= _callStack.Length)
        {
            Array.Resize(ref _callStack, _callStack.Length * 2);
        }
        _callStack[_callStackPtr++] = frame;
    }

    public CallFrame PopCallFrame()
    {
        if (_callStackPtr <= 0) throw new Exception("Call stack underflow");
        var frame = _callStack[--_callStackPtr];
        _callStack[_callStackPtr] = default; // Clear reference
        return frame;
    }

    internal bool HandleException(ScriptRuntimeException e)
    {
        if (TryStack.Count > 0)
        {
            var tryBlock = TryStack.Pop();

            // Unwind CallStack
            Array.Clear(_callStack, tryBlock.CallStackDepth, _callStackPtr - tryBlock.CallStackDepth);
            _callStackPtr = tryBlock.CallStackDepth;

            // Restore stack pointer
            _stackPtr = tryBlock.StackPointer;

            // Set catch variable if needed
            if (tryBlock.CatchReference.HasValue)
            {
                var catchValue = e.ThrownValue ?? new DreamValue(e.Message);
                SetReferenceValue(tryBlock.CatchReference.Value, _callStack[_callStackPtr - 1], catchValue, 0);
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
                if (reference.Index < 0 || reference.Index >= frame.Proc.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", frame.Proc, 0, this);
                return _stack[frame.StackBase + reference.Index];
            case DMReference.Type.Local:
                if (reference.Index < 0 || reference.Index >= frame.Proc.LocalVariableCount) throw new ScriptRuntimeException("Local index out of bounds", frame.Proc, 0, this);
                return _stack[frame.StackBase + frame.Proc.Arguments.Length + reference.Index];
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
    public void SetReferenceValue(DMReference reference, CallFrame frame, DreamValue value, int stackOffset = 0)
    {
        switch (reference.RefType)
        {
            case DMReference.Type.Global:
                Context.SetGlobal(reference.Index, value);
                break;
            case DMReference.Type.Argument:
                if (reference.Index < 0 || reference.Index >= frame.Proc.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", frame.Proc, 0, this);
                _stack[frame.StackBase + reference.Index] = value;
                break;
            case DMReference.Type.Local:
                if (reference.Index < 0 || reference.Index >= frame.Proc.LocalVariableCount) throw new ScriptRuntimeException("Local index out of bounds", frame.Proc, 0, this);
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
