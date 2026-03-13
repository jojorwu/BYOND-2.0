using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using Shared;
using Core.VM.Procs;

namespace Core.VM.Runtime;

/// <summary>
/// Specialized internal structure for managing the VM value stack.
/// Provides high-performance access and encapsulated growth logic.
/// </summary>
internal struct DreamStack : IDisposable
{
    public DreamValue[] Array;
    public int Pointer;

    public DreamStack(int initialCapacity)
    {
        Array = ArrayPool<DreamValue>.Shared.Rent(initialCapacity);
        Pointer = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(DreamValue value, int maxStackSize, IDreamProc currentProc, int pc, DreamThread thread)
    {
        if ((uint)Pointer >= (uint)maxStackSize)
            throw new ScriptRuntimeException("Stack overflow", currentProc, pc, thread);

        if (Pointer >= Array.Length)
            EnsureCapacity(1, maxStackSize);

        Array[Pointer++] = value;
    }

    public DreamValue this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Array[index];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Array[index] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<DreamValue> AsSpan(int start, int length)
    {
        return Array.AsSpan(start, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DreamValue Pop()
    {
        return Array[--Pointer];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int required, int maxStackSize)
    {
        if (Pointer + required >= Array.Length)
        {
            int minSize = Pointer + required;
            int newSize = Array.Length == 0 ? 1024 : Array.Length * 2;
            while (newSize < minSize) newSize *= 2;

            newSize = Math.Min(newSize, maxStackSize);
            if (newSize <= Array.Length && minSize <= Array.Length) return;
            if (minSize > maxStackSize) throw new InvalidOperationException("Stack size limit reached");

            var newStack = ArrayPool<DreamValue>.Shared.Rent(newSize);
            System.Array.Copy(Array, newStack, Pointer);
            ArrayPool<DreamValue>.Shared.Return(Array, true);
            Array = newStack;
        }
    }

    public void Dispose()
    {
        if (Array != null)
        {
            ArrayPool<DreamValue>.Shared.Return(Array, true);
            Array = null!;
        }
    }
}
