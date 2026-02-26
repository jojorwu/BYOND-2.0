using System;
using System.Runtime.CompilerServices;
using Robust.Shared.Maths;

namespace Shared;

/// <summary>
/// A class containing operations used by both the compiler and the server.
/// Helps make sure things like sin() and cos() give the same result on both.
/// </summary>
public static class SharedOperations {
    private const float DegToRad = MathF.PI / 180.0f;
    private const float RadToDeg = 180.0f / MathF.PI;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sin(float x) {
        return MathF.Sin(x * DegToRad);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Cos(float x) {
        return MathF.Cos(x * DegToRad);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Tan(float x) {
        return MathF.Tan(x * DegToRad);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ArcSin(float x) {
        return MathF.Asin(x) * RadToDeg;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ArcCos(float x) {
        return MathF.Acos(x) * RadToDeg;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ArcTan(float a) {
        return MathF.Atan(a) * RadToDeg;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ArcTan(float x, float y) {
        return MathF.Atan2(y, x) * RadToDeg;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sqrt(float a) {
        return MathF.Sqrt(a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Log(float y) {
        return MathF.Log(y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Log(float y, float baseValue) {
        return MathF.Log(y, baseValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Abs(float a) {
        return MathF.Abs(a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Modulo(float a, float b) {
        if (b == 0) return 0;
        return a - b * MathF.Floor(a / b);
    }

    //because BYOND has everything as a 32 bit float with 8 bit mantissa, we need to chop off the
    //top 8 bits when bit shifting for parity.
    //We also handle negative shift amounts (by swapping left/right) and large shift amounts (resulting in 0).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int BitShiftLeft(int left, int right) {
        if (right <= 0) return (right == 0) ? (left & 0x00FFFFFF) : BitShiftRight(left, -right);
        if (right >= 24) return 0;
        return (left << right) & 0x00FFFFFF;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int BitShiftRight(int left, int right) {
        if (right <= 0) return (right == 0) ? (left & 0x00FFFFFF) : BitShiftLeft(left, -right);
        if (right >= 24) return 0;
        return (left & 0x00FFFFFF) >> right;
    }

    public enum ColorSpace {
        RGB = 0,
        HSV = 1,
        HSL = 2
    }

    public static string ParseRgb(ReadOnlySpan<(string? Name, float? Value)> arguments) {
        if (arguments.Length == 0)
            throw new Exception("Expected at least 3 arguments for rgb()");

        float? color1 = null;
        float? color2 = null;
        float? color3 = null;
        float? alpha = null;
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
                float h = Math.Clamp(color1.Value, 0, 360) / 360f;
                float s = Math.Clamp(color2.Value, 0, 100) / 100f;
                float v = Math.Clamp(color3.Value, 0, 100) / 100f;

                color = Color.FromHsv(new(h, s, v, aValue / 255f));
                break;
            }
            case ColorSpace.HSL: {
                float h = Math.Clamp(color1.Value, 0, 360) / 360f;
                float s = Math.Clamp(color2.Value, 0, 100) / 100f;
                float l = Math.Clamp(color3.Value, 0, 100) / 100f;

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
