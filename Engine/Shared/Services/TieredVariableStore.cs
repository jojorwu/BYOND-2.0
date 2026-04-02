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
public class TieredVariableStore : IObservableVariableStore
{
    private const int SparseThreshold = 16;
    private DreamValue[]? _defaults;
    private DreamValue[] _overrides = Array.Empty<DreamValue>();
    private ulong[] _modifiedMask = Array.Empty<ulong>();
    private int[]? _sparseIndices;
    private DreamValue[]? _sparseValues;
    private int _sparseCount;
    private int _length;
    private IVariableChangeListener[] _listeners = Array.Empty<IVariableChangeListener>();
    private readonly System.Threading.Lock _listenerLock = new();
    private IGameObject? _owner;

    public int Length => _length;

    public void SetOwner(IGameObject owner) => _owner = owner;

    public void Subscribe(IVariableChangeListener listener)
    {
        lock (_listenerLock)
        {
            var updated = new IVariableChangeListener[_listeners.Length + 1];
            _listeners.CopyTo(updated, 0);
            updated[_listeners.Length] = listener;
            _listeners = updated;
        }
    }

    public void Unsubscribe(IVariableChangeListener listener)
    {
        lock (_listenerLock)
        {
            int index = Array.IndexOf(_listeners, listener);
            if (index == -1) return;
            var updated = new IVariableChangeListener[_listeners.Length - 1];
            Array.Copy(_listeners, 0, updated, 0, index);
            Array.Copy(_listeners, index + 1, updated, index, _listeners.Length - index - 1);
            _listeners = updated;
        }
    }

    public void VisitModified(IVariableStore.Visitor visitor)
    {
        if (_sparseIndices != null)
        {
            for (int i = 0; i < _sparseCount; i++) visitor(_sparseIndices[i], _sparseValues![i]);
            return;
        }

        for (int i = 0; i < _modifiedMask.Length; i++)
        {
            ulong mask = _modifiedMask[i];
            if (mask == 0) continue;

            int baseIdx = i << 6;
            while (mask != 0)
            {
                int bit = System.Numerics.BitOperations.TrailingZeroCount(mask);
                int index = baseIdx + bit;
                if (index < _length)
                {
                    visitor(index, _overrides[index]);
                }
                mask &= mask - 1;
            }
        }
    }

    public void VisitModified<T>(ref T visitor) where T : struct, IVariableVisitor, allows ref struct
    {
        if (_sparseIndices != null)
        {
            for (int i = 0; i < _sparseCount; i++) visitor.Visit(_sparseIndices[i], _sparseValues![i]);
            return;
        }

        for (int i = 0; i < _modifiedMask.Length; i++)
        {
            ulong mask = _modifiedMask[i];
            if (mask == 0) continue;

            int baseIdx = i << 6;
            while (mask != 0)
            {
                int bit = System.Numerics.BitOperations.TrailingZeroCount(mask);
                int index = baseIdx + bit;
                if (index < _length)
                {
                    visitor.Visit(index, _overrides[index]);
                }
                mask &= mask - 1;
            }
        }
    }

    public void Initialize(int capacity)
    {
        _length = capacity;
        int maskSize = (capacity + 63) / 64;
        if (_modifiedMask.Length < maskSize)
        {
             if (_modifiedMask.Length > 0) ArrayPool<ulong>.Shared.Return(_modifiedMask);
             _modifiedMask = ArrayPool<ulong>.Shared.Rent(maskSize);
        }
        Array.Clear(_modifiedMask, 0, _modifiedMask.Length);

        if (_overrides.Length < capacity)
        {
            if (_overrides.Length > 0) ArrayPool<DreamValue>.Shared.Return(_overrides, true);
            _overrides = ArrayPool<DreamValue>.Shared.Rent(capacity);
        }
        Array.Clear(_overrides, 0, _overrides.Length);
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

        if (_sparseIndices != null)
        {
            for (int i = 0; i < _sparseCount; i++)
            {
                if (_sparseIndices[i] == index) return _sparseValues![i];
            }
        }
        else if (IsModified(index))
        {
            return _overrides[index];
        }

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
        if (_modifiedMask.Length > 0)
        {
            if ((uint)index >= (uint)_overrides.Length) Expand(index + 1);
            _overrides[index] = value;
            int word = index >> 6;
            int bit = index & 63;
            _modifiedMask[word] |= (1UL << bit);
        }
        else
        {
            // Sparse/Tiered logic
            int idx = -1;
            if (_sparseIndices != null)
            {
                for (int i = 0; i < _sparseCount; i++)
                {
                    if (_sparseIndices[i] == index) { idx = i; break; }
                }
            }

            if (idx != -1)
            {
                _sparseValues![idx] = value;
            }
            else
            {
                if (_sparseCount < SparseThreshold)
                {
                    _sparseIndices ??= new int[SparseThreshold];
                    _sparseValues ??= new DreamValue[SparseThreshold];
                    _sparseIndices[_sparseCount] = index;
                    _sparseValues[_sparseCount] = value;
                    _sparseCount++;
                }
                else
                {
                    // Promote to dense
                    var denseCapacity = Math.Max(index + 1, _length);
                    Expand(denseCapacity);
                    if (_sparseIndices != null)
                    {
                        for (int i = 0; i < _sparseCount; i++)
                        {
                            int sIdx = _sparseIndices[i];
                            _overrides[sIdx] = _sparseValues![i];
                            _modifiedMask[sIdx >> 6] |= (1UL << (sIdx & 63));
                        }
                        _sparseIndices = null;
                        _sparseValues = null;
                        _sparseCount = 0;
                    }
                    _overrides[index] = value;
                    _modifiedMask[index >> 6] |= (1UL << (index & 63));
                }
            }
        }

        if (index >= _length) _length = index + 1;

        var listeners = _listeners;
        if (listeners.Length > 0 && _owner != null)
        {
            for (int i = 0; i < listeners.Length; i++)
            {
                listeners[i].OnVariableChanged(_owner, index, value);
            }
        }
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

    public void ClearModified()
    {
        if (_modifiedMask.Length > 0) Array.Clear(_modifiedMask, 0, _modifiedMask.Length);
        _sparseCount = 0;
    }

    public void Dispose()
    {
        if (_overrides.Length > 0)
        {
            Array.Clear(_overrides, 0, _overrides.Length);
            ArrayPool<DreamValue>.Shared.Return(_overrides, true);
            _overrides = Array.Empty<DreamValue>();
        }
        if (_modifiedMask.Length > 0)
        {
            Array.Clear(_modifiedMask, 0, _modifiedMask.Length);
            ArrayPool<ulong>.Shared.Return(_modifiedMask);
            _modifiedMask = Array.Empty<ulong>();
        }
        _sparseIndices = null;
        _sparseValues = null;
        _sparseCount = 0;
        _defaults = null;
        _owner = null;
        _listeners = Array.Empty<IVariableChangeListener>();
        _length = 0;
    }
}
