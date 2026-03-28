using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Shared.Models;

/// <summary>
/// A memory-efficient bitmask representing a set of components.
/// Uses a small fixed-size ulong array to avoid heap allocations for most compositions.
/// </summary>
public struct ComponentMask : IEquatable<ComponentMask>
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
    }

    public void Unset(int index)
    {
        uint idx = (uint)index;
        if (idx < 64) _mask0 &= ~(1UL << (int)idx);
        else if (idx < 128) _mask1 &= ~(1UL << (int)(idx - 64));
        else if (idx < 192) _mask2 &= ~(1UL << (int)(idx - 128));
        else if (idx < 256) _mask3 &= ~(1UL << (int)(idx - 192));
        else if (idx < 320) _mask4 &= ~(1UL << (int)(idx - 256));
        else if (idx < 384) _mask5 &= ~(1UL << (int)(idx - 320));
        else if (idx < 448) _mask6 &= ~(1UL << (int)(idx - 384));
        else if (idx < 512) _mask7 &= ~(1UL << (int)(idx - 448));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IntersectWith(ComponentMask other)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            Unsafe.As<ulong, Vector512<ulong>>(ref _mask0) &= Unsafe.As<ulong, Vector512<ulong>>(ref Unsafe.AsRef(in other._mask0));
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            Unsafe.As<ulong, Vector256<ulong>>(ref _mask0) &= Unsafe.As<ulong, Vector256<ulong>>(ref Unsafe.AsRef(in other._mask0));
            Unsafe.As<ulong, Vector256<ulong>>(ref _mask4) &= Unsafe.As<ulong, Vector256<ulong>>(ref Unsafe.AsRef(in other._mask4));
        }
        else
        {
            _mask0 &= other._mask0; _mask1 &= other._mask1; _mask2 &= other._mask2; _mask3 &= other._mask3;
            _mask4 &= other._mask4; _mask5 &= other._mask5; _mask6 &= other._mask6; _mask7 &= other._mask7;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnionWith(ComponentMask other)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            Unsafe.As<ulong, Vector512<ulong>>(ref _mask0) |= Unsafe.As<ulong, Vector512<ulong>>(ref Unsafe.AsRef(in other._mask0));
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            Unsafe.As<ulong, Vector256<ulong>>(ref _mask0) |= Unsafe.As<ulong, Vector256<ulong>>(ref Unsafe.AsRef(in other._mask0));
            Unsafe.As<ulong, Vector256<ulong>>(ref _mask4) |= Unsafe.As<ulong, Vector256<ulong>>(ref Unsafe.AsRef(in other._mask4));
        }
        else
        {
            _mask0 |= other._mask0; _mask1 |= other._mask1; _mask2 |= other._mask2; _mask3 |= other._mask3;
            _mask4 |= other._mask4; _mask5 |= other._mask5; _mask6 |= other._mask6; _mask7 |= other._mask7;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Not()
    {
        if (Vector512.IsHardwareAccelerated)
        {
            Unsafe.As<ulong, Vector512<ulong>>(ref _mask0) = ~Unsafe.As<ulong, Vector512<ulong>>(ref _mask0);
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            Unsafe.As<ulong, Vector256<ulong>>(ref _mask0) = ~Unsafe.As<ulong, Vector256<ulong>>(ref _mask0);
            Unsafe.As<ulong, Vector256<ulong>>(ref _mask4) = ~Unsafe.As<ulong, Vector256<ulong>>(ref _mask4);
        }
        else
        {
            _mask0 = ~_mask0; _mask1 = ~_mask1; _mask2 = ~_mask2; _mask3 = ~_mask3;
            _mask4 = ~_mask4; _mask5 = ~_mask5; _mask6 = ~_mask6; _mask7 = ~_mask7;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        if (Vector512.IsHardwareAccelerated)
        {
            Unsafe.As<ulong, Vector512<ulong>>(ref _mask0) = Vector512<ulong>.Zero;
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            Unsafe.As<ulong, Vector256<ulong>>(ref _mask0) = Vector256<ulong>.Zero;
            Unsafe.As<ulong, Vector256<ulong>>(ref _mask4) = Vector256<ulong>.Zero;
        }
        else
        {
            _mask0 = _mask1 = _mask2 = _mask3 = _mask4 = _mask5 = _mask6 = _mask7 = 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsAll(ComponentMask other)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            var v = Unsafe.As<ulong, Vector512<ulong>>(ref _mask0);
            var ov = Unsafe.As<ulong, Vector512<ulong>>(ref Unsafe.AsRef(in other._mask0));
            return Vector512.EqualsAll(v & ov, ov);
        }
        if (Vector256.IsHardwareAccelerated)
        {
            var v0 = Unsafe.As<ulong, Vector256<ulong>>(ref _mask0);
            var ov0 = Unsafe.As<ulong, Vector256<ulong>>(ref Unsafe.AsRef(in other._mask0));
            var v1 = Unsafe.As<ulong, Vector256<ulong>>(ref _mask4);
            var ov1 = Unsafe.As<ulong, Vector256<ulong>>(ref Unsafe.AsRef(in other._mask4));

            return Vector256.EqualsAll(v0 & ov0, ov0) && Vector256.EqualsAll(v1 & ov1, ov1);
        }

        return (_mask0 & other._mask0) == other._mask0 &&
               (_mask1 & other._mask1) == other._mask1 &&
               (_mask2 & other._mask2) == other._mask2 &&
               (_mask3 & other._mask3) == other._mask3 &&
               (_mask4 & other._mask4) == other._mask4 &&
               (_mask5 & other._mask5) == other._mask5 &&
               (_mask6 & other._mask6) == other._mask6 &&
               (_mask7 & other._mask7) == other._mask7;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Overlaps(ComponentMask other)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            var v = Unsafe.As<ulong, Vector512<ulong>>(ref _mask0) & Unsafe.As<ulong, Vector512<ulong>>(ref Unsafe.AsRef(in other._mask0));
            return v != Vector512<ulong>.Zero;
        }
        if (Vector256.IsHardwareAccelerated)
        {
            var v0 = Unsafe.As<ulong, Vector256<ulong>>(ref _mask0) & Unsafe.As<ulong, Vector256<ulong>>(ref Unsafe.AsRef(in other._mask0));
            var v1 = Unsafe.As<ulong, Vector256<ulong>>(ref _mask4) & Unsafe.As<ulong, Vector256<ulong>>(ref Unsafe.AsRef(in other._mask4));
            return !Vector256.EqualsAll(v0 | v1, Vector256<ulong>.Zero);
        }

        return (_mask0 & other._mask0) != 0 ||
               (_mask1 & other._mask1) != 0 ||
               (_mask2 & other._mask2) != 0 ||
               (_mask3 & other._mask3) != 0 ||
               (_mask4 & other._mask4) != 0 ||
               (_mask5 & other._mask5) != 0 ||
               (_mask6 & other._mask6) != 0 ||
               (_mask7 & other._mask7) != 0;
    }

    public bool IsEmpty => (_mask0 | _mask1 | _mask2 | _mask3 | _mask4 | _mask5 | _mask6 | _mask7) == 0;

    public int Count => BitOperations.PopCount(_mask0) + BitOperations.PopCount(_mask1) +
                        BitOperations.PopCount(_mask2) + BitOperations.PopCount(_mask3) +
                        BitOperations.PopCount(_mask4) + BitOperations.PopCount(_mask5) +
                        BitOperations.PopCount(_mask6) + BitOperations.PopCount(_mask7);

    public bool Get(int index)
    {
        uint idx = (uint)index;
        if (idx < 64) return (_mask0 & (1UL << (int)idx)) != 0;
        if (idx < 128) return (_mask1 & (1UL << (int)(idx - 64))) != 0;
        if (idx < 192) return (_mask2 & (1UL << (int)(idx - 128))) != 0;
        if (idx < 256) return (_mask3 & (1UL << (int)(idx - 192))) != 0;
        if (idx < 320) return (_mask4 & (1UL << (int)(idx - 256))) != 0;
        if (idx < 384) return (_mask5 & (1UL << (int)(idx - 320))) != 0;
        if (idx < 448) return (_mask6 & (1UL << (int)(idx - 384))) != 0;
        if (idx < 512) return (_mask7 & (1UL << (int)(idx - 448))) != 0;
        return false;
    }

    /// <summary>
    /// Enumerates indices of all set bits in the mask without heap allocations.
    /// </summary>
    public Enumerator GetSetBits() => new Enumerator(this);

    public struct Enumerator
    {
        private ComponentMask _mask;
        private int _current;

        public Enumerator(ComponentMask mask)
        {
            _mask = mask;
            _current = -1;
        }

        public bool MoveNext()
        {
            if (_mask._mask0 != 0)
            {
                _current = BitOperations.TrailingZeroCount(_mask._mask0);
                _mask._mask0 &= _mask._mask0 - 1;
                return true;
            }
            if (_mask._mask1 != 0)
            {
                _current = BitOperations.TrailingZeroCount(_mask._mask1) + 64;
                _mask._mask1 &= _mask._mask1 - 1;
                return true;
            }
            if (_mask._mask2 != 0)
            {
                _current = BitOperations.TrailingZeroCount(_mask._mask2) + 128;
                _mask._mask2 &= _mask._mask2 - 1;
                return true;
            }
            if (_mask._mask3 != 0)
            {
                _current = BitOperations.TrailingZeroCount(_mask._mask3) + 192;
                _mask._mask3 &= _mask._mask3 - 1;
                return true;
            }
            if (_mask._mask4 != 0)
            {
                _current = BitOperations.TrailingZeroCount(_mask._mask4) + 256;
                _mask._mask4 &= _mask._mask4 - 1;
                return true;
            }
            if (_mask._mask5 != 0)
            {
                _current = BitOperations.TrailingZeroCount(_mask._mask5) + 320;
                _mask._mask5 &= _mask._mask5 - 1;
                return true;
            }
            if (_mask._mask6 != 0)
            {
                _current = BitOperations.TrailingZeroCount(_mask._mask6) + 384;
                _mask._mask6 &= _mask._mask6 - 1;
                return true;
            }
            if (_mask._mask7 != 0)
            {
                _current = BitOperations.TrailingZeroCount(_mask._mask7) + 448;
                _mask._mask7 &= _mask._mask7 - 1;
                return true;
            }
            return false;
        }

        public int Current => _current;

        public Enumerator GetEnumerator() => this;
    }

    public bool Equals(ComponentMask other)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            return Vector512.EqualsAll(Unsafe.As<ulong, Vector512<ulong>>(ref _mask0), Unsafe.As<ulong, Vector512<ulong>>(ref Unsafe.AsRef(in other._mask0)));
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            var v0 = Vector256.EqualsAll(Unsafe.As<ulong, Vector256<ulong>>(ref _mask0), Unsafe.As<ulong, Vector256<ulong>>(ref Unsafe.AsRef(in other._mask0)));
            var v1 = Vector256.EqualsAll(Unsafe.As<ulong, Vector256<ulong>>(ref _mask4), Unsafe.As<ulong, Vector256<ulong>>(ref Unsafe.AsRef(in other._mask4)));
            return v0 && v1;
        }

        return _mask0 == other._mask0 && _mask1 == other._mask1 &&
               _mask2 == other._mask2 && _mask3 == other._mask3 &&
               _mask4 == other._mask4 && _mask5 == other._mask5 &&
               _mask6 == other._mask6 && _mask7 == other._mask7;
    }

    public override bool Equals(object? obj) => obj is ComponentMask other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_mask0); hash.Add(_mask1); hash.Add(_mask2); hash.Add(_mask3);
        hash.Add(_mask4); hash.Add(_mask5); hash.Add(_mask6); hash.Add(_mask7);
        return hash.ToHashCode();
    }

    public static bool operator ==(ComponentMask left, ComponentMask right) => left.Equals(right);
    public static bool operator !=(ComponentMask left, ComponentMask right) => !left.Equals(right);
}
