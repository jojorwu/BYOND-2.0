using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Shared.Utils;

/// <summary>
/// A high-performance bit-level writer for compact serialization.
/// </summary>
public ref struct BitWriter
{
    private Span<byte> _destination;
    private int _bitOffset;

    public BitWriter(Span<byte> destination)
    {
        _destination = destination;
        _bitOffset = 0;
    }

    public int BitsWritten => _bitOffset;
    public int BytesWritten => (_bitOffset + 7) / 8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBits(ulong value, int bitCount)
    {
        if (bitCount == 0) return;

        while (bitCount > 0)
        {
            int byteIdx = _bitOffset / 8;
            int bitInByteIdx = _bitOffset % 8;
            int bitsToWriteInByte = Math.Min(bitCount, 8 - bitInByteIdx);

            ulong mask = (1UL << bitsToWriteInByte) - 1;
            ulong bits = (value >> (bitCount - bitsToWriteInByte)) & mask;

            if (bitInByteIdx == 0) _destination[byteIdx] = 0;
            _destination[byteIdx] |= (byte)(bits << (8 - bitInByteIdx - bitsToWriteInByte));

            _bitOffset += bitsToWriteInByte;
            bitCount -= bitsToWriteInByte;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBool(bool value)
    {
        WriteBits(value ? 1UL : 0UL, 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt(int value, int bitCount)
    {
        WriteBits((ulong)value, bitCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLong(long value, int bitCount)
    {
        WriteBits((ulong)value, bitCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDouble(double value)
    {
        WriteBits(BitConverter.DoubleToUInt64Bits(value), 64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteVarInt(long value)
    {
        ulong v = (ulong)value;
        while (v >= 0x80)
        {
            WriteBool(true);
            WriteBits(v & 0x7F, 7);
            v >>= 7;
        }
        WriteBool(false);
        WriteBits(v & 0x7F, 7);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteZigZag(long value)
    {
        ulong zigzag = (ulong)((value << 1) ^ (value >> 63));
        WriteVarInt((long)zigzag);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteString(string? s)
    {
        if (string.IsNullOrEmpty(s))
        {
            WriteVarInt(0);
        }
        else
        {
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            WriteVarInt(bytes.Length);
            foreach (var b in bytes) WriteBits(b, 8);
        }
    }
}
