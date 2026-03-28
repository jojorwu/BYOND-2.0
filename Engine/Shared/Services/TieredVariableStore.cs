using Shared;
using Shared.Interfaces;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Shared.Services;

/// <summary>
/// A memory-efficient variable store that utilizes default values from an ObjectType
/// and only stores modified values in a local flat array.
/// </summary>
public class TieredVariableStore : IVariableStore
{
    private DreamValue[]? _defaults;
    private DreamValue[] _overrides = Array.Empty<DreamValue>();
    private ulong[] _modifiedMask = Array.Empty<ulong>();
    private int _length;

    public int Length => _length;

    public void Initialize(int capacity)
    {
        _length = capacity;
        int maskSize = (capacity + 63) / 64;
        if (_modifiedMask.Length < maskSize)
        {
             if (_modifiedMask.Length > 0) ArrayPool<ulong>.Shared.Return(_modifiedMask);
             _modifiedMask = ArrayPool<ulong>.Shared.Rent(maskSize);
        }
        Array.Clear(_modifiedMask, 0, maskSize);

        if (_overrides.Length < capacity)
        {
            if (_overrides.Length > 0) ArrayPool<DreamValue>.Shared.Return(_overrides, true);
            _overrides = ArrayPool<DreamValue>.Shared.Rent(capacity);
        }
        Array.Clear(_overrides, 0, capacity);
    }

    public void SetDefaults(DreamValue[] defaults)
    {
        _defaults = defaults;
        if (defaults.Length > _length) _length = defaults.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DreamValue Get(int index)
    {
        if ((uint)index >= (uint)_length) return DreamValue.Null;

        if (IsModified(index)) return _overrides[index];
        if (_defaults != null && (uint)index < (uint)_defaults.Length) return _defaults[index];

        return DreamValue.Null;
    }

    public ref DreamValue GetRef(int index)
    {
        // Tiered store doesn't easily support direct refs to defaults.
        // We ensure it's in overrides if a ref is requested.
        EnsureModified(index);
        return ref _overrides[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsModified(int index)
    {
        int word = index >> 6;
        int bit = index & 63;
        return word < _modifiedMask.Length && (_modifiedMask[word] & (1UL << bit)) != 0;
    }

    private void EnsureModified(int index)
    {
        if (IsModified(index)) return;

        var val = Get(index);
        Set(index, val);
    }

    public void Set(int index, DreamValue value)
    {
        if ((uint)index >= (uint)_overrides.Length)
        {
            Expand(index + 1);
        }

        _overrides[index] = value;
        int word = index >> 6;
        int bit = index & 63;
        _modifiedMask[word] |= (1UL << bit);
        if (index >= _length) _length = index + 1;
    }

    private void Expand(int minCapacity)
    {
        int newCapacity = _overrides.Length == 0 ? 8 : _overrides.Length * 2;
        while (newCapacity < minCapacity) newCapacity *= 2;

        var newOverrides = ArrayPool<DreamValue>.Shared.Rent(newCapacity);
        if (_overrides.Length > 0)
        {
            _overrides.AsSpan(0, _overrides.Length).CopyTo(newOverrides);
            ArrayPool<DreamValue>.Shared.Return(_overrides, true);
        }
        _overrides = newOverrides;

        int newMaskSize = (newCapacity + 63) / 64;
        var newMask = ArrayPool<ulong>.Shared.Rent(newMaskSize);
        Array.Clear(newMask, 0, newMask.Length);
        if (_modifiedMask.Length > 0)
        {
            _modifiedMask.AsSpan(0, _modifiedMask.Length).CopyTo(newMask);
            ArrayPool<ulong>.Shared.Return(_modifiedMask);
        }
        _modifiedMask = newMask;
    }

    public void CopyFrom(DreamValue[] source)
    {
        Initialize(source.Length);
        source.AsSpan().CopyTo(_overrides);
        // Mark all as modified since we are copying a full state
        for (int i = 0; i < _modifiedMask.Length; i++) _modifiedMask[i] = ~0UL;
    }

    public void Dispose()
    {
        if (_overrides.Length > 0)
        {
            ArrayPool<DreamValue>.Shared.Return(_overrides, true);
            _overrides = Array.Empty<DreamValue>();
        }
        if (_modifiedMask.Length > 0)
        {
            ArrayPool<ulong>.Shared.Return(_modifiedMask);
            _modifiedMask = Array.Empty<ulong>();
        }
        _defaults = null;
        _length = 0;
    }
}
