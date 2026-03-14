using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Shared.Models;

/// <summary>
/// A high-performance bitmask for tracking resource access (Read/Write) during system execution planning.
/// Utilizes fixed-size storage to eliminate allocations in the planning hot path.
/// </summary>
public struct ResourceMask : IEquatable<ResourceMask>
{
    private ulong _mask0;
    private ulong _mask1;

    public void Set(int index)
    {
        if ((uint)index < 64) _mask0 |= (1UL << index);
        else if ((uint)index < 128) _mask1 |= (1UL << (index - 64));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnionWith(ResourceMask other)
    {
        _mask0 |= other._mask0;
        _mask1 |= other._mask1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Overlaps(ResourceMask other)
    {
        return (_mask0 & other._mask0) != 0 ||
               (_mask1 & other._mask1) != 0;
    }

    public bool Supports(int index) => (uint)index < 128;

    public bool IsEmpty => _mask0 == 0 && _mask1 == 0;

    public bool Equals(ResourceMask other)
    {
        return _mask0 == other._mask0 && _mask1 == other._mask1;
    }

    public override bool Equals(object? obj) => obj is ResourceMask other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_mask0, _mask1);

    public static bool operator ==(ResourceMask left, ResourceMask right) => left.Equals(right);
    public static bool operator !=(ResourceMask left, ResourceMask right) => !left.Equals(right);
}
