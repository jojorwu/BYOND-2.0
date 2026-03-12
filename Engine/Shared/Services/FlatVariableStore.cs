using Shared;
using Shared.Interfaces;
using System;
using System.Runtime.CompilerServices;

namespace Shared.Services;

public class FlatVariableStore : IVariableStore
{
    private DreamValue[] _values = Array.Empty<DreamValue>();

    public int Length => _values.Length;

    public void Initialize(int capacity)
    {
        if (_values.Length < capacity)
        {
            _values = new DreamValue[capacity];
        }
    }

    public DreamValue Get(int index)
    {
        var vals = _values;
        if ((uint)index < (uint)vals.Length)
            return vals[index];
        return DreamValue.Null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref DreamValue GetRef(int index)
    {
        return ref _values[index];
    }

    public void Set(int index, DreamValue value)
    {
        if ((uint)index >= (uint)_values.Length)
        {
            int newSize = _values.Length == 0 ? 8 : _values.Length * 2;
            while (newSize <= index) newSize *= 2;

            var newValues = new DreamValue[newSize];
            if (_values.Length > 0) _values.AsSpan().CopyTo(newValues);
            _values = newValues;
        }
        _values[index] = value;
    }

    public void CopyFrom(DreamValue[] source)
    {
        if (_values.Length != source.Length)
        {
            _values = new DreamValue[source.Length];
        }
        source.AsSpan().CopyTo(_values);
    }

    public void Dispose()
    {
        _values = Array.Empty<DreamValue>();
    }
}
