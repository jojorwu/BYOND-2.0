using System;

namespace Shared;

/// <summary>
/// A 64-bit integer bounding box used for massive spatial queries.
/// Replaces Box2i to support coordinates up to $10^{12}$ and beyond.
/// </summary>
public readonly struct Box2l : IEquatable<Box2l>
{
    public readonly long Left;
    public readonly long Bottom;
    public readonly long Right;
    public readonly long Top;

    public Box2l(long left, long bottom, long right, long top)
    {
        Left = left;
        Bottom = bottom;
        Right = right;
        Top = top;
    }

    public bool Equals(Box2l other) => Left == other.Left && Bottom == other.Bottom && Right == other.Right && Top == other.Top;
    public override bool Equals(object? obj) => obj is Box2l other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Left, Bottom, Right, Top);

    public static bool operator ==(Box2l left, Box2l right) => left.Equals(right);
    public static bool operator !=(Box2l left, Box2l right) => !left.Equals(right);
}
