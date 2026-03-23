using System;
using System.Runtime.InteropServices;

namespace Robust.Shared.Maths
{
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

        public override readonly string ToString() => $"({X}, {Y}, {Z})";
    }
}
