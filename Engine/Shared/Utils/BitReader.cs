using System;
using System.Runtime.CompilerServices;

namespace Shared.Utils;

/// <summary>
/// A high-performance bit-level reader for compact serialization.
/// </summary>
public ref struct BitReader
{
    private ReadOnlySpan<byte> _source;
    private int _bitOffset;

    public BitReader(ReadOnlySpan<byte> source)
    {
        _source = source;
        _bitOffset = 0;
    }

    public int BitsRead => _bitOffset;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadBits(int bitCount)
    {
        if (bitCount == 0) return 0;

        ulong result = 0;
        while (bitCount > 0)
        {
            int byteIdx = _bitOffset / 8;
            int bitInByteIdx = _bitOffset % 8;
            int bitsToReadInByte = Math.Min(bitCount, 8 - bitInByteIdx);

            ulong mask = (1UL << bitsToReadInByte) - 1;
            ulong bits = (ulong)(_source[byteIdx] >> (8 - bitInByteIdx - bitsToReadInByte)) & mask;

            result = (result << bitsToReadInByte) | bits;

            _bitOffset += bitsToReadInByte;
            bitCount -= bitsToReadInByte;
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadBool()
    {
        return ReadBits(1) == 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt(int bitCount)
    {
        return (int)ReadBits(bitCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadLong(int bitCount)
    {
        return (long)ReadBits(bitCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadDouble()
    {
        return BitConverter.UInt64BitsToDouble(ReadBits(64));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadVarInt()
    {
        long result = 0;
        int shift = 0;
        while (ReadBool())
        {
            result |= (long)ReadBits(7) << shift;
            shift += 7;
        }
        result |= (long)ReadBits(7) << shift;
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadZigZag()
    {
        ulong zigzag = (ulong)ReadVarInt();
        return (long)(zigzag >> 1) ^ -(long)(zigzag & 1);
    }
}
