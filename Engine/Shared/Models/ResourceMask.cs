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
    private ulong _mask2;
    private ulong _mask3;
    private ulong _mask4;
    private ulong _mask5;
    private ulong _mask6;
    private ulong _mask7;

    public void Set(int index)
    {
        uint idx = (uint)index;
        if (idx < 64) _mask0 |= (1UL << (int)idx);
        else if (idx < 128) _mask1 |= (1UL << (int)(idx - 64));
        else if (idx < 192) _mask2 |= (1UL << (int)(idx - 128));
        else if (idx < 256) _mask3 |= (1UL << (int)(idx - 192));
        else if (idx < 320) _mask4 |= (1UL << (int)(idx - 256));
        else if (idx < 384) _mask5 |= (1UL << (int)(idx - 320));
        else if (idx < 448) _mask6 |= (1UL << (int)(idx - 384));
        else if (idx < 512) _mask7 |= (1UL << (int)(idx - 448));
        else throw new ArgumentOutOfRangeException(nameof(index), $"Resource index {index} exceeds the supported mask limit (512).");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnionWith(ResourceMask other)
    {
        _mask0 |= other._mask0;
        _mask1 |= other._mask1;
        _mask2 |= other._mask2;
        _mask3 |= other._mask3;
        _mask4 |= other._mask4;
        _mask5 |= other._mask5;
        _mask6 |= other._mask6;
        _mask7 |= other._mask7;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Overlaps(ResourceMask other)
    {
        return (_mask0 & other._mask0) != 0 ||
               (_mask1 & other._mask1) != 0 ||
               (_mask2 & other._mask2) != 0 ||
               (_mask3 & other._mask3) != 0 ||
               (_mask4 & other._mask4) != 0 ||
               (_mask5 & other._mask5) != 0 ||
               (_mask6 & other._mask6) != 0 ||
               (_mask7 & other._mask7) != 0;
    }

    public void Clear()
    {
        _mask0 = _mask1 = _mask2 = _mask3 = _mask4 = _mask5 = _mask6 = _mask7 = 0;
    }

    public bool Supports(int index) => (uint)index < 512;

    public bool IsEmpty => (_mask0 | _mask1 | _mask2 | _mask3 | _mask4 | _mask5 | _mask6 | _mask7) == 0;

    public bool Equals(ResourceMask other)
    {
        return _mask0 == other._mask0 && _mask1 == other._mask1 &&
               _mask2 == other._mask2 && _mask3 == other._mask3 &&
               _mask4 == other._mask4 && _mask5 == other._mask5 &&
               _mask6 == other._mask6 && _mask7 == other._mask7;
    }

    public override bool Equals(object? obj) => obj is ResourceMask other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_mask0);
        hash.Add(_mask1);
        hash.Add(_mask2);
        hash.Add(_mask3);
        hash.Add(_mask4);
        hash.Add(_mask5);
        hash.Add(_mask6);
        hash.Add(_mask7);
        return hash.ToHashCode();
    }

    public static bool operator ==(ResourceMask left, ResourceMask right) => left.Equals(right);
    public static bool operator !=(ResourceMask left, ResourceMask right) => !left.Equals(right);
}
