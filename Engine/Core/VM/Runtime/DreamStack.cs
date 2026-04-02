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

        var array = Array;
        if ((uint)Pointer >= (uint)array.Length)
        {
            EnsureCapacity(1, maxStackSize);
            array = Array;
        }

        System.Runtime.CompilerServices.Unsafe.Add(ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(array), Pointer++) = value;
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
    public void FastFillNull(int start, int count)
    {
        if (count <= 0) return;
        Array.AsSpan(start, count).Fill(DreamValue.Null);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DreamValue Pop()
    {
        ref var valRef = ref System.Runtime.CompilerServices.Unsafe.Add(ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(Array), --Pointer);
        var val = valRef;
        valRef = default; // Clear slot to prevent stale references for GC
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int required, int maxStackSize)
    {
        if (Pointer + required > Array.Length)
        {
            Expand(required, maxStackSize);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Expand(int required, int maxStackSize)
    {
        int minSize = Pointer + required;
        if (minSize > maxStackSize) throw new InvalidOperationException("Stack size limit reached");

        // Aggressive growth: 2x expansion with a minimum jump to 4096 to reduce early pool cycles
        int newSize = Array.Length == 0 ? 4096 : Array.Length * 2;
        while (newSize < minSize) newSize *= 2;
        newSize = Math.Min(newSize, maxStackSize);

        var newStack = ArrayPool<DreamValue>.Shared.Rent(newSize);
        System.Array.Copy(Array, newStack, Pointer);
        ArrayPool<DreamValue>.Shared.Return(Array, true);
        Array = newStack;
    }

    public void Reset()
    {
        if (Array != null)
        {
            System.Array.Clear(Array, 0, Pointer);
            Pointer = 0;
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
