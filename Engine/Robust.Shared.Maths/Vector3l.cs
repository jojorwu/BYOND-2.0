using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Robust.Shared.Maths;

[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct Vector3l : IEquatable<Vector3l>
{
    public long X;
    public long Y;
    public long Z;

    public static readonly Vector3l Zero = new(0, 0, 0);

    public Vector3l(long x, long y, long z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Vector3l(long value)
    {
        X = value;
        Y = value;
        Z = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Deconstruct(out long x, out long y, out long z)
    {
        x = X;
        y = Y;
        z = Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Vector3l other)
    {
        return X == other.X && Y == other.Y && Z == other.Z;
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is Vector3l other && Equals(other);
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z);
    }

    public static bool operator ==(Vector3l left, Vector3l right) => left.Equals(right);
    public static bool operator !=(Vector3l left, Vector3l right) => !left.Equals(right);

    public static Vector3l operator +(Vector3l left, Vector3l right) => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
    public static Vector3l operator -(Vector3l left, Vector3l right) => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    public static Vector3l operator *(Vector3l left, long right) => new(left.X * right, left.Y * right, left.Z * right);
    public static Vector3l operator /(Vector3l left, long right) => new(left.X / right, left.Y / right, left.Z / right);

    public override readonly string ToString() => $"({X}, {Y}, {Z})";
}
