using Shared.Enums;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Core.VM.Procs;
using Shared;
using Shared.Interfaces;

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
public partial class DreamThread : IScriptThread, IDisposable, IPoolable
{
    public const int MaxCallStackDepth = 65536;
    public const int MaxStackSize = 10485760;
    internal DreamStack _stack;
    internal int _stackPtr { get => _stack.Pointer; set => _stack.Pointer = value; }
    internal CallFrame[] _callStack = ArrayPool<CallFrame>.Shared.Rent(1024);
    internal int _callStackPtr = 0;
    private TryBlock[] _tryStack = ArrayPool<TryBlock>.Shared.Rent(1024);
    private int _tryStackPtr = 0;

    internal void PushTryBlock(TryBlock tryBlock)
    {
        if (_tryStackPtr >= _tryStack.Length)
        {
            var newStack = ArrayPool<TryBlock>.Shared.Rent(_tryStack.Length * 2);
            Array.Copy(_tryStack, newStack, _tryStack.Length);
            ArrayPool<TryBlock>.Shared.Return(_tryStack, true);
            _tryStack = newStack;
        }
        _tryStack[_tryStackPtr++] = tryBlock;
    }

    internal void PopTryBlock()
    {
        if (_tryStackPtr > 0)
        {
            _tryStackPtr--;
            _tryStack[_tryStackPtr] = default; // Clear reference (DMReference.Name)
        }
    }

    private record struct EnumeratorEntry(IEnumerator<DreamValue>? Enumerator, DreamList? List);
    private EnumeratorEntry[] _enumerators = ArrayPool<EnumeratorEntry>.Shared.Rent(64);
    private int _maxEnumeratorId = -1;

    private void EnsureEnumeratorCapacity(int id)
    {
        if (id >= _enumerators.Length)
        {
            int newSize = _enumerators.Length;
            while (newSize <= id) newSize *= 2;
            var newArr = ArrayPool<EnumeratorEntry>.Shared.Rent(newSize);
            Array.Copy(_enumerators, newArr, _enumerators.Length);
            Array.Clear(newArr, _enumerators.Length, newArr.Length - _enumerators.Length);
            ArrayPool<EnumeratorEntry>.Shared.Return(_enumerators, true);
            _enumerators = newArr;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerator<DreamValue>? GetEnumerator(int id)
    {
        return (id >= 0 && id < _enumerators.Length) ? _enumerators[id].Enumerator : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetEnumerator(int id, IEnumerator<DreamValue> enumerator, DreamList? list)
    {
        if (id < 0) return;
        EnsureEnumeratorCapacity(id);
        _enumerators[id] = new EnumeratorEntry(enumerator, list);
        if (id > _maxEnumeratorId) _maxEnumeratorId = id;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DreamList? GetEnumeratorList(int id)
    {
        return (id >= 0 && id < _enumerators.Length) ? _enumerators[id].List : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveEnumerator(int id)
    {
        if (id >= 0 && id < _enumerators.Length)
        {
            _enumerators[id].Enumerator?.Dispose();
            _enumerators[id] = default;
        }
    }
    public int StackCount => _stackPtr;
    public int CallStackCount => _callStackPtr;

    public IEnumerable<CallFrame> CallStack => _callStack.Take(_callStackPtr).Reverse();

    public DreamProc? CurrentProc => _callStackPtr > 0 ? _callStack[_callStackPtr - 1].Proc : null;
    private volatile int _state = (int)DreamThreadState.Running;
    public DreamThreadState State { get => (DreamThreadState)_state; internal set => _state = (int)value; }
    public DateTime SleepUntil { get; internal set; }
    private IGameObject? _associatedObject;
    public IGameObject? AssociatedObject => _associatedObject;
    public DreamObject? Usr { get; set; }

    public ScriptThreadPriority Priority { get; set; } = ScriptThreadPriority.Normal;
    public TimeSpan ExecutionTime { get; set; } = TimeSpan.Zero;
    public int WaitTicks { get; set; } = 0;
    public long TotalInstructionsExecuted => _totalInstructionsExecuted;
    public int InstructionQuotaBalance { get; set; } = 0;

    public DreamVMContext? Context { get; private set; }
    internal int _maxInstructions;
    internal long _totalInstructionsExecuted;
    private int _maxCallStackReached;
    private readonly IBytecodeInterpreter _interpreter;

    public DreamThread(DreamProc proc, DreamVMContext context, int maxInstructions, IGameObject? associatedObject = null, IBytecodeInterpreter? interpreter = null)
    {
        _interpreter = interpreter ?? new BytecodeInterpreter();
        Initialize(proc, context, maxInstructions, associatedObject, interpreter);
    }

    public DreamThread()
    {
        _interpreter = new BytecodeInterpreter();
    }

    public void Initialize(DreamProc proc, DreamVMContext context, int maxInstructions, IGameObject? associatedObject = null, IBytecodeInterpreter? interpreter = null)
    {
        Context = context;
        _maxInstructions = maxInstructions;
        _associatedObject = associatedObject;
        if (_stack.Array == null) _stack = new DreamStack(Math.Max(1024, proc.LocalVariableCount));

        PushCallFrame(new CallFrame(proc, 0, 0, associatedObject as DreamObject));

        // Initialize locals for the entry-point frame using optimized fast-fill
        int localCount = proc.LocalVariableCount;
        if (localCount > 0)
        {
            _stack.Pointer = localCount;
            _stack.FastFillNull(0, localCount);
        }
    }

    public DreamThread(DreamThread other, int pc)
    {
        Context = other.Context;
        _maxInstructions = other._maxInstructions;
        _interpreter = other._interpreter;
        _associatedObject = other._associatedObject;

        // Rent and copy stack
        _stack = new DreamStack(other._stack.Array.Length);
        Array.Copy(other._stack.Array, _stack.Array, other._stack.Pointer);
        _stack.Pointer = other._stack.Pointer;

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
        var depth = _callStackPtr;
        var stack = _callStack;

        if ((uint)depth >= (uint)stack.Length)
        {
            if ((uint)depth >= (uint)MaxCallStackDepth)
                throw new ScriptRuntimeException("Max call stack depth exceeded", frame.Proc, frame.PC, this);

            ExpandCallStack();
            stack = _callStack;
        }

        System.Runtime.CompilerServices.Unsafe.Add(ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(stack), depth) = frame;
        _callStackPtr = depth + 1;
        if (_callStackPtr > _maxCallStackReached) _maxCallStackReached = _callStackPtr;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ExpandCallStack()
    {
        int newSize = _callStack.Length * 2;
        if (newSize > MaxCallStackDepth) newSize = MaxCallStackDepth;

        var newStack = ArrayPool<CallFrame>.Shared.Rent(newSize);
        _callStack.AsSpan(0, _callStackPtr).CopyTo(newStack);
        ArrayPool<CallFrame>.Shared.Return(_callStack, true);
        _callStack = newStack;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CallFrame PopCallFrame()
    {
        int depth = _callStackPtr;
        if ((uint)depth <= 0) throw new Exception("Call stack underflow");

        ref var frameRef = ref System.Runtime.CompilerServices.Unsafe.Add(ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(_callStack), --depth);
        var frame = frameRef;

        // Ensure we clear any lists or objects in the frame to avoid pinning
        frameRef = default;
        _callStackPtr = depth;
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
            _stack.Pointer = tryBlock.StackPointer;

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


    public DreamThreadState Run(int instructionBudget)
    {
        return _interpreter.Run(this, instructionBudget);
    }

    public void Reset()
    {
        if (_callStack != null)
        {
            Array.Clear(_callStack, 0, _callStackPtr);
            _callStackPtr = 0;
        }

        if (_tryStack != null)
        {
            Array.Clear(_tryStack, 0, _tryStackPtr);
            _tryStackPtr = 0;
        }

        if (_enumerators != null)
        {
            for (int i = 0; i <= _maxEnumeratorId; i++)
            {
                _enumerators[i].Enumerator?.Dispose();
                _enumerators[i] = default;
            }
            _maxEnumeratorId = -1;
        }

        Usr = null;
        _associatedObject = null;
        if (_stack.Array != null) _stack.Reset();

        _totalInstructionsExecuted = 0;
        _maxCallStackReached = 0;
        ExecutionTime = TimeSpan.Zero;
        WaitTicks = 0;
        Priority = ScriptThreadPriority.Normal;
        State = DreamThreadState.Running;
    }

    public void Dispose()
    {
        if (_callStack != null)
        {
            Array.Clear(_callStack, 0, _callStack.Length);
            ArrayPool<CallFrame>.Shared.Return(_callStack);
            _callStack = null!;
        }

        if (_tryStack != null)
        {
            Array.Clear(_tryStack, 0, _tryStack.Length);
            ArrayPool<TryBlock>.Shared.Return(_tryStack);
            _tryStack = null!;
        }

        if (_enumerators != null)
        {
            for (int i = 0; i <= _maxEnumeratorId; i++)
            {
                _enumerators[i].Enumerator?.Dispose();
            }
            Array.Clear(_enumerators, 0, _enumerators.Length);
            ArrayPool<EnumeratorEntry>.Shared.Return(_enumerators, true);
            _enumerators = null!;
        }

        Usr = null;

        _stack.Dispose();

        GC.SuppressFinalize(this);
    }

    ~DreamThread()
    {
        Dispose();
    }
}
