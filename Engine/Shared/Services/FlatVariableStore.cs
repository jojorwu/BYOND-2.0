using Shared;
using Shared.Interfaces;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Shared.Services;

public class FlatVariableStore : IVariableStore
{
    private DreamValue[] _values = Array.Empty<DreamValue>();
    private int _length;

    public int Length => _length;

    public void Initialize(int capacity)
    {
        if (_values.Length < capacity)
        {
            if (_values.Length > 0)
            {
                ArrayPool<DreamValue>.Shared.Return(_values, clearArray: true);
            }
            _values = ArrayPool<DreamValue>.Shared.Rent(capacity);
        }

        // Ensure the rented array is cleared before use to prevent data leakage from previous owners
        Array.Clear(_values, 0, capacity);
        _length = capacity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DreamValue Get(int index)
    {
        if ((uint)index < (uint)_length)
            return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_values), index);
        return DreamValue.Null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref DreamValue GetRef(int index)
    {
        return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_values), index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int index, DreamValue value)
    {
        if ((uint)index >= (uint)_values.Length)
        {
            SetSlow(index, value);
            return;
        }

        if (index >= _length) _length = index + 1;
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_values), index) = value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SetSlow(int index, DreamValue value)
    {
        int newCapacity = _values.Length == 0 ? 8 : _values.Length * 2;
        while (newCapacity <= index) newCapacity *= 2;

        var newValues = ArrayPool<DreamValue>.Shared.Rent(newCapacity);
        Array.Clear(newValues, 0, newValues.Length);

        if (_length > 0)
        {
            _values.AsSpan(0, _length).CopyTo(newValues);
            ArrayPool<DreamValue>.Shared.Return(_values, clearArray: true);
        }
        _values = newValues;

        if (index >= _length) _length = index + 1;
        _values[index] = value;
    }

    public void CopyFrom(DreamValue[] source)
    {
        if (_values.Length < source.Length)
        {
            if (_values.Length > 0) ArrayPool<DreamValue>.Shared.Return(_values, clearArray: true);
            _values = ArrayPool<DreamValue>.Shared.Rent(source.Length);
        }
        source.AsSpan().CopyTo(_values);
        _length = source.Length;
    }

    public void Dispose()
    {
        if (_values.Length > 0)
        {
            ArrayPool<DreamValue>.Shared.Return(_values, clearArray: true);
            _values = Array.Empty<DreamValue>();
        }
        _length = 0;
    }
}
