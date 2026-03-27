using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Robust.Shared.Maths;

[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct Box3l : IEquatable<Box3l>
{
    public long Left;
    public long Bottom;
    public long Back;
    public long Right;
    public long Top;
    public long Front;

    public Box3l(long left, long bottom, long back, long right, long top, long front)
    {
        Left = left;
        Bottom = bottom;
        Back = back;
        Right = right;
        Top = top;
        Front = front;
    }

    public Box3l(Vector3l min, Vector3l max)
    {
        Left = min.X;
        Bottom = min.Y;
        Back = min.Z;
        Right = max.X;
        Top = max.Y;
        Front = max.Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Contains(Vector3l point)
    {
        return point.X >= Left && point.X <= Right &&
               point.Y >= Bottom && point.Y <= Top &&
               point.Z >= Back && point.Z <= Front;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Box3l other)
    {
        return Left == other.Left && Bottom == other.Bottom && Back == other.Back &&
               Right == other.Right && Top == other.Top && Front == other.Front;
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is Box3l other && Equals(other);
    }

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Left); hash.Add(Bottom); hash.Add(Back);
        hash.Add(Right); hash.Add(Top); hash.Add(Front);
        return hash.ToHashCode();
    }

    public static bool operator ==(Box3l left, Box3l right) => left.Equals(right);
    public static bool operator !=(Box3l left, Box3l right) => !left.Equals(right);

    public override readonly string ToString() => $"({Left}, {Bottom}, {Back}, {Right}, {Top}, {Front})";
}
