using Shared.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Core.VM.Procs;
using Shared;

namespace Core.VM.Runtime;

/// <summary>
/// A high-performance ref struct used to capture the interpreter's mutable state.
/// By using a ref struct and capturing the current Frame as a ref, we eliminate
/// struct copying overhead during tight execution loops.
/// </summary>
internal unsafe ref struct InterpreterState
{
    public DreamThread Thread;
    /// <summary>
    /// A reference to the current execution frame on the thread's call stack.
    /// </summary>
    public ref CallFrame Frame;
    public DreamProc Proc;
    public int PC;
    public Span<DreamValue> Stack;
    public Span<DreamValue> Locals;
    public Span<DreamValue> Arguments;
    public int LocalBase;
    public int ArgumentBase;
    public int StackPtr;
    public byte[] BytecodeArray;
    /// <summary>
    /// Fixed pointer to the bytecode array to bypass Bounds Checks and BinaryPrimitives overhead.
    /// </summary>
    public byte* BytecodePtr;
    public List<string> Strings;
    public IList<DreamValue> Globals;
    public Dictionary<string, IDreamProc> Procs;
    public DreamObject? World;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(DreamValue value)
    {
        var ptr = StackPtr;
        var stack = Stack;
        if ((uint)ptr >= (uint)stack.Length)
        {
            ExpandAndPush(value);
            return;
        }
        stack[ptr] = value;
        StackPtr = ptr + 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ExpandAndPush(DreamValue value)
    {
        Thread._stackPtr = StackPtr;
        Thread.Push(value);
        RefreshSpans();
        StackPtr = Thread._stackPtr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RefreshSpans()
    {
        var array = Thread._stack.Array;
        // Ensure the Stack span does not exceed MaxStackSize so that Push fast-path correctly triggers overflow handling
        int limit = Math.Min(array.Length, DreamThread.MaxStackSize);
        Stack = array.AsSpan(0, limit);

        LocalBase = Frame.LocalBase;
        ArgumentBase = Frame.ArgumentBase;
        Locals = Stack.Slice(LocalBase, Proc.LocalVariableCount);
        Arguments = Stack.Slice(ArgumentBase, Proc.Arguments.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureStack(int count)
    {
        if (StackPtr + count >= Stack.Length || StackPtr + count >= DreamThread.MaxStackSize)
        {
            ExpandStack(count);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ExpandStack(int count)
    {
        Thread._stackPtr = StackPtr;
        Thread.EnsureStackCapacity(count);
        RefreshSpans();
        StackPtr = Thread._stackPtr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DreamValue Pop()
    {
        var ptr = StackPtr;
        if (ptr <= 0)
        {
            return HandlePopUnderflow();
        }
        ptr--;
        StackPtr = ptr;
        return Stack[ptr];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private DreamValue HandlePopUnderflow()
    {
        throw new ScriptRuntimeException("Stack underflow during Pop", Proc, PC, Thread);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadByte()
    {
        if (PC >= BytecodeArray.Length) throw new ScriptRuntimeException("Read past end of bytecode", Proc, PC, Thread);
        return BytecodePtr[PC++];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt32()
    {
        if (PC + 4 > BytecodeArray.Length) throw new ScriptRuntimeException("Read past end of bytecode", Proc, PC, Thread);
        var value = *(int*)(BytecodePtr + PC);
        PC += 4;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadDouble()
    {
        if (PC + 8 > BytecodeArray.Length) throw new ScriptRuntimeException("Read past end of bytecode", Proc, PC, Thread);
        var value = *(double*)(BytecodePtr + PC);
        PC += 8;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref DreamValue GetLocal(int index)
    {
        return ref Locals[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref DreamValue GetArgument(int index)
    {
        return ref Arguments[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DMReference ReadReference()
    {
        var refType = (DMReference.Type)BytecodePtr[PC++];
        if (refType == DMReference.Type.Local || refType == DMReference.Type.Argument)
        {
            var idx = *(int*)(BytecodePtr + PC);
            PC += 4;
            return new DMReference { RefType = refType, Index = idx };
        }

        if (refType >= DMReference.Type.Global && refType <= DMReference.Type.GlobalProc)
        {
            var globalIdx = *(int*)(BytecodePtr + PC);
            PC += 4;
            return new DMReference { RefType = refType, Index = globalIdx };
        }

        if (refType >= DMReference.Type.Field && refType <= DMReference.Type.SrcField)
        {
            var nameId = *(int*)(BytecodePtr + PC);
            PC += 4;
            if ((uint)nameId >= (uint)Thread.Context.Strings.Count) throw new ScriptRuntimeException($"Invalid string ID: {nameId}", Proc, PC - 5, Thread);
            return new DMReference { RefType = refType, Name = Thread.Context.Strings[nameId] };
        }

        return new DMReference { RefType = refType };
    }
}
