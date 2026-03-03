using System;
using System.Runtime.CompilerServices;
using Robust.Shared.Maths;

namespace Shared;

/// <summary>
/// A class containing operations used by both the compiler and the server.
/// Helps make sure things like sin() and cos() give the same result on both.
/// </summary>
public static class SharedOperations {
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Sin(double x) {
        return Math.Sin(x * DegToRad);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Cos(double x) {
        return Math.Cos(x * DegToRad);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Tan(double x) {
        return Math.Tan(x * DegToRad);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ArcSin(double x) {
        return Math.Asin(x) * RadToDeg;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ArcCos(double x) {
        return Math.Acos(x) * RadToDeg;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ArcTan(double a) {
        return Math.Atan(a) * RadToDeg;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ArcTan(double x, double y) {
        return Math.Atan2(y, x) * RadToDeg;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Sqrt(double a) {
        return Math.Sqrt(a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Log(double y) {
        return Math.Log(y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Log(double y, double baseValue) {
        return Math.Log(y, baseValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Abs(double a) {
        return Math.Abs(a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Modulo(double a, double b) {
        if (b == 0) return 0;
        return a - b * Math.Floor(a / b);
    }

    // Removed the 24-bit shift limit and 0x00FFFFFF mask to support the fundamental 64-bit architecture transition.
    // We also handle negative shift amounts (by swapping left/right) and large shift amounts (resulting in 0).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long BitShiftLeft(long left, long right) {
        if (right <= 0) return (right == 0) ? left : BitShiftRight(left, -right);
        if (right >= 64) return 0;
        return left << (int)right;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long BitShiftRight(long left, long right) {
        if (right <= 0) return (right == 0) ? left : BitShiftLeft(left, -right);
        if (right >= 64) return 0;
        return (long)((ulong)left >> (int)right);
    }

    public enum ColorSpace {
        RGB = 0,
        HSV = 1,
        HSL = 2
    }

    public static string ParseRgb(ReadOnlySpan<(string? Name, double? Value)> arguments) {
        if (arguments.Length == 0)
            throw new Exception("Expected at least 3 arguments for rgb()");

        double? color1 = null;
        double? color2 = null;
        double? color3 = null;
        double? alpha = null;
        ColorSpace space = ColorSpace.RGB;

        if (arguments[0].Name is null) {
            if (arguments.Length < 3)
                throw new Exception("Expected at least 3 arguments for rgb()");

            color1 = arguments[0].Value;
            color2 = arguments.Length > 1 ? arguments[1].Value : null;
            color3 = arguments.Length > 2 ? arguments[2].Value : null;
            alpha = arguments.Length > 3 ? arguments[3].Value : null;

            if (arguments.Length > 4)
                space = arguments[4].Value is null ? ColorSpace.RGB : (ColorSpace)(int)arguments[4].Value!;
        } else {
            foreach (var arg in arguments) {
                var name = arg.Name;
                if (name == null) continue;

                if (name.StartsWith("r", StringComparison.OrdinalIgnoreCase) && color1 is null) {
                    color1 = arg.Value;
                    space = ColorSpace.RGB;
                } else if (name.StartsWith("g", StringComparison.OrdinalIgnoreCase) && color2 is null) {
                    color2 = arg.Value;
                    space = ColorSpace.RGB;
                } else if (name.StartsWith("b", StringComparison.OrdinalIgnoreCase) && color3 is null) {
                    color3 = arg.Value;
                    space = ColorSpace.RGB;
                } else if (name.StartsWith("h", StringComparison.OrdinalIgnoreCase) && color1 is null) {
                    color1 = arg.Value;
                    space = ColorSpace.HSV;
                } else if (name.StartsWith("s", StringComparison.OrdinalIgnoreCase) && color2 is null) {
                    color2 = arg.Value;
                    space = ColorSpace.HSV;
                } else if (name.StartsWith("v", StringComparison.OrdinalIgnoreCase) && color3 is null) {
                    color3 = arg.Value;
                    space = ColorSpace.HSV;
                } else if (name.StartsWith("l", StringComparison.OrdinalIgnoreCase) && color3 is null) {
                    color3 = arg.Value;
                    space = ColorSpace.HSL;
                } else if (name.StartsWith("a", StringComparison.OrdinalIgnoreCase) && alpha is null) {
                    alpha = arg.Value;
                } else if (name.Equals("space", StringComparison.OrdinalIgnoreCase)) {
                    space = (ColorSpace)(int)(arg.Value ?? 0);
                } else {
                    throw new Exception($"Invalid or double arg \"{name}\"");
                }
            }
        }

        color1 ??= 0;
        color2 ??= 0;
        color3 ??= 0;
        byte aValue = alpha is null ? (byte)255 : (byte)Math.Clamp((int)alpha, 0, 255);
        Color color;

        switch (space) {
            case ColorSpace.RGB: {
                byte r = (byte)Math.Clamp(color1.Value, 0, 255);
                byte g = (byte)Math.Clamp(color2.Value, 0, 255);
                byte b = (byte)Math.Clamp(color3.Value, 0, 255);

                color = new Color(r, g, b, aValue);
                break;
            }
            case ColorSpace.HSV: {
                float h = (float)(Math.Clamp(color1.Value, 0, 360) / 360.0);
                float s = (float)(Math.Clamp(color2.Value, 0, 100) / 100.0);
                float v = (float)(Math.Clamp(color3.Value, 0, 100) / 100.0);

                color = Color.FromHsv(new(h, s, v, aValue / 255f));
                break;
            }
            case ColorSpace.HSL: {
                float h = (float)(Math.Clamp(color1.Value, 0, 360) / 360.0);
                float s = (float)(Math.Clamp(color2.Value, 0, 100) / 100.0);
                float l = (float)(Math.Clamp(color3.Value, 0, 100) / 100.0);

                color = Color.FromHsl(new(h, s, l, aValue / 255f));
                break;
            }
            default:
                throw new NotSupportedException($"Unimplemented color space {space}");
        }

        return alpha is null
            ? string.Create(7, color, (span, c) => {
                span[0] = '#';
                c.RByte.TryFormat(span.Slice(1, 2), out _, "x2");
                c.GByte.TryFormat(span.Slice(3, 2), out _, "x2");
                c.BByte.TryFormat(span.Slice(5, 2), out _, "x2");
            })
            : string.Create(9, color, (span, c) => {
                span[0] = '#';
                c.RByte.TryFormat(span.Slice(1, 2), out _, "x2");
                c.GByte.TryFormat(span.Slice(3, 2), out _, "x2");
                c.BByte.TryFormat(span.Slice(5, 2), out _, "x2");
                c.AByte.TryFormat(span.Slice(7, 2), out _, "x2");
            });
    }
}
