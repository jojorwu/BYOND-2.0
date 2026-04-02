using Shared.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

    /// <summary>
    /// Managed reference to the start of the thread's value stack.
    /// Utilizes C# 11 'ref fields' to bypass Span-based bounds checks.
    /// </summary>
    public ref DreamValue StackBase;

    /// <summary>
    /// Managed reference to the first local variable of the current frame.
    /// </summary>
    public ref DreamValue LocalBase;

    /// <summary>
    /// Managed reference to the first argument of the current frame.
    /// </summary>
    public ref DreamValue ArgumentBase;

    public int StackPtr;
    public byte[] BytecodeArray;
    /// <summary>
    /// Fixed pointer to the bytecode array to bypass Bounds Checks and BinaryPrimitives overhead.
    /// </summary>
    public byte* BytecodePtr;
    public ReadOnlySpan<string> Strings;
    public DreamVMContext Context;
    public System.Collections.Concurrent.ConcurrentDictionary<string, IDreamProc> Procs;
    public DreamObject? World;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(DreamValue value)
    {
        var ptr = StackPtr;
        // Verify capacity against both the logical limit and the physical backing array length.
        // We MUST check MaxStackSize first to ensure we throw a ScriptRuntimeException
        // before potentially exceeding it due to ArrayPool returning larger-than-requested buffers.
        if ((uint)ptr >= (uint)DreamThread.MaxStackSize || (uint)ptr >= (uint)Thread._stack.Array.Length)
        {
            ExpandAndPush(value);
            return;
        }

        Unsafe.Add(ref StackBase, ptr) = value;
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
        ref var arrayRef = ref MemoryMarshal.GetArrayDataReference(array);

        StackBase = ref arrayRef;
        LocalBase = ref Unsafe.Add(ref arrayRef, Frame.LocalBase);
        ArgumentBase = ref Unsafe.Add(ref arrayRef, Frame.ArgumentBase);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureStack(int count)
    {
        if (StackPtr + count >= Thread._stack.Array.Length || StackPtr + count >= DreamThread.MaxStackSize)
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
        ref var valRef = ref Unsafe.Add(ref StackBase, ptr);
        var val = valRef;
        valRef = default; // Clear reference for GC
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref DreamValue Peek()
    {
        if (StackPtr <= 0) throw new ScriptRuntimeException("Stack underflow during Peek", Proc, PC, Thread);
        return ref Unsafe.Add(ref StackBase, StackPtr - 1);
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
        var value = Unsafe.ReadUnaligned<int>(BytecodePtr + PC);
        PC += 4;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadDouble()
    {
        if (PC + 8 > BytecodeArray.Length) throw new ScriptRuntimeException("Read past end of bytecode", Proc, PC, Thread);
        var value = Unsafe.ReadUnaligned<double>(BytecodePtr + PC);
        PC += 8;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref DreamValue GetLocal(int index)
    {
        if ((uint)index >= (uint)Proc.LocalVariableCount) throw new ScriptRuntimeException("Local index out of bounds", Proc, PC, Thread);
        return ref Unsafe.Add(ref LocalBase, index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref DreamValue GetArgument(int index)
    {
        if ((uint)index >= (uint)Proc.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", Proc, PC, Thread);
        return ref Unsafe.Add(ref ArgumentBase, index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref DreamValue GetStack(int index)
    {
        return ref Unsafe.Add(ref StackBase, index);
    }

    public Span<DreamValue> StackSpan => MemoryMarshal.CreateSpan(ref StackBase, Thread._stack.Array.Length);
    public Span<DreamValue> LocalSpan => MemoryMarshal.CreateSpan(ref LocalBase, Proc.LocalVariableCount);
    public Span<DreamValue> ArgumentSpan => MemoryMarshal.CreateSpan(ref ArgumentBase, Proc.Arguments.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DMReference ReadReference()
    {
        if (PC >= BytecodeArray.Length) throw new ScriptRuntimeException("Read past end of bytecode", Proc, PC, Thread);
        var refType = (DMReference.Type)BytecodePtr[PC++];
        if (refType == DMReference.Type.Local || refType == DMReference.Type.Argument)
        {
            if (PC + 4 > BytecodeArray.Length) throw new ScriptRuntimeException("Read past end of bytecode", Proc, PC, Thread);
            var idx = Unsafe.ReadUnaligned<int>(BytecodePtr + PC);
            PC += 4;
            return new DMReference { RefType = refType, Index = idx };
        }

        if (refType >= DMReference.Type.Global && refType <= DMReference.Type.GlobalProc)
        {
            if (PC + 4 > BytecodeArray.Length) throw new ScriptRuntimeException("Read past end of bytecode", Proc, PC, Thread);
            var globalIdx = Unsafe.ReadUnaligned<int>(BytecodePtr + PC);
            PC += 4;
            return new DMReference { RefType = refType, Index = globalIdx };
        }

        if (refType >= DMReference.Type.Field && refType <= DMReference.Type.SrcField)
        {
            if (PC + 4 > BytecodeArray.Length) throw new ScriptRuntimeException("Read past end of bytecode", Proc, PC, Thread);
            var nameId = Unsafe.ReadUnaligned<int>(BytecodePtr + PC);
            PC += 4;
            if ((uint)nameId >= (uint)Strings.Length) throw new ScriptRuntimeException($"Invalid string ID: {nameId}", Proc, PC - 5, Thread);
            return new DMReference { RefType = refType, Name = Strings[nameId] };
        }

        return new DMReference { RefType = refType };
    }
}
