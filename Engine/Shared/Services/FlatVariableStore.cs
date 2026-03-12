using Shared;
using Shared.Interfaces;
using System;

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
        if (index >= 0 && index < _values.Length)
            return _values[index];
        return DreamValue.Null;
    }

    public void Set(int index, DreamValue value)
    {
        if (index >= _values.Length)
        {
            var newValues = new DreamValue[index + 1];
            Array.Copy(_values, newValues, _values.Length);
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
        Array.Copy(source, _values, source.Length);
    }

    public void Dispose()
    {
        _values = Array.Empty<DreamValue>();
    }
}
