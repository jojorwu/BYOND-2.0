using System;
using System.Collections;
using System.Runtime.CompilerServices;

namespace Shared.Models;

/// <summary>
/// A memory-efficient bitmask representing a set of components.
/// Uses a small fixed-size ulong array to avoid heap allocations for most compositions.
/// </summary>
public struct ComponentMask : IEquatable<ComponentMask>
{
    private ulong _mask0;
    private ulong _mask1;
    // For more than 128 components, we could use an internal array,
    // but 128 is usually enough for entity composition.

    public void Set(int index)
    {
        if (index < 64) _mask0 |= (1UL << index);
        else if (index < 128) _mask1 |= (1UL << (index - 64));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsAll(ComponentMask other)
    {
        return (_mask0 & other._mask0) == other._mask0 &&
               (_mask1 & other._mask1) == other._mask1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Overlaps(ComponentMask other)
    {
        return (_mask0 & other._mask0) != 0 ||
               (_mask1 & other._mask1) != 0;
    }

    public bool Equals(ComponentMask other)
    {
        return _mask0 == other._mask0 && _mask1 == other._mask1;
    }

    public override bool Equals(object? obj) => obj is ComponentMask other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_mask0, _mask1);

    public static bool operator ==(ComponentMask left, ComponentMask right) => left.Equals(right);
    public static bool operator !=(ComponentMask left, ComponentMask right) => !left.Equals(right);
}
