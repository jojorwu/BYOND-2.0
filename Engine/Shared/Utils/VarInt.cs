using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Shared.Utils;

/// <summary>
/// Centralized utility for VarInt encoding and decoding.
/// </summary>
public static class VarInt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSize(long value)
    {
        ulong v = (ulong)value;
        int count = 1;
        while (v >= 0x80)
        {
            v >>= 7;
            count++;
        }
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Write(Span<byte> span, long value)
    {
        ulong v = (ulong)value;
        int count = 0;
        while (v >= 0x80)
        {
            span[count++] = (byte)(v | 0x80);
            v >>= 7;
        }
        span[count++] = (byte)v;
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Read(ReadOnlySpan<byte> span, out int bytesRead)
    {
        long result = 0;
        int shift = 0;
        bytesRead = 0;
        while (bytesRead < span.Length)
        {
            byte b = span[bytesRead++];
            result |= (long)(b & 0x7f) << shift;
            if ((b & 0x80) == 0)
            {
                if (shift > 63 || (shift == 63 && (b & 0x7e) != 0))
                    throw new InvalidDataException("VarInt too large for 64-bit integer");
                return result;
            }
            shift += 7;
            if (shift >= 70) throw new InvalidDataException("Malformed VarInt: too many bytes");
        }
        throw new InvalidDataException("Unexpected end of stream while reading VarInt");
    }
}
