using System;
using System.Runtime.InteropServices;

namespace Robust.Shared.Maths
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct Box3l : IEquatable<Box3l>
    {
        public Vector3l Min;
        public Vector3l Max;

        public long Left => Min.X;
        public long Bottom => Min.Y;
        public long Front => Min.Z;
        public long Right => Max.X;
        public long Top => Max.Y;
        public long Back => Max.Z;

        public Box3l(Vector3l min, Vector3l max)
        {
            Min = min;
            Max = max;
        }

        public Box3l(long left, long bottom, long front, long right, long top, long back)
        {
            Min = new Vector3l(left, bottom, front);
            Max = new Vector3l(right, top, back);
        }

        public readonly bool Contains(Vector3l point)
        {
            return point.X >= Min.X && point.X <= Max.X &&
                   point.Y >= Min.Y && point.Y <= Max.Y &&
                   point.Z >= Min.Z && point.Z <= Max.Z;
        }

        public readonly bool Equals(Box3l other)
        {
            return Min.Equals(other.Min) && Max.Equals(other.Max);
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is Box3l other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(Min, Max);
        }

        public static bool operator ==(Box3l left, Box3l right) => left.Equals(right);
        public static bool operator !=(Box3l left, Box3l right) => !left.Equals(right);

        public override readonly string ToString() => $"[({Min.X}, {Min.Y}, {Min.Z}), ({Max.X}, {Max.Y}, {Max.Z})]";
    }
}
