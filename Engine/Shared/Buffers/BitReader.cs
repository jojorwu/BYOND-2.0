using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Shared.Buffers;

/// <summary>
/// A high-performance bit-level reader for compact serialization.
/// </summary>
public ref struct BitReader
{
    private ReadOnlySpan<byte> _source;
    private int _bitOffset;

    public BitReader(ReadOnlySpan<ReadOnlyMemory<byte>> segments)
    {
        // For simplicity, we currently flatten segments or use a single span.
        // A multi-segment reader would be more complex and require tracking segment transitions.
        throw new NotSupportedException("Multi-segment reader is not yet implemented.");
    }

    public BitReader(ReadOnlySpan<byte> source)
    {
        _source = source;
        _bitOffset = 0;
    }

    public int BitsRead => _bitOffset;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int bitsNeeded)
    {
        if (_bitOffset + bitsNeeded > _source.Length * 8)
        {
            throw new IndexOutOfRangeException($"BitReader overflow. Offset: {_bitOffset}, Needed: {bitsNeeded}, Capacity: {_source.Length * 8}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadBits(int bitCount)
    {
        if (bitCount == 0) return 0;
        EnsureCapacity(bitCount);

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
    public void SkipBits(int bitCount)
    {
        _bitOffset += bitCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadByte()
    {
        return (byte)ReadBits(8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadFloat()
    {
        return BitConverter.Int32BitsToSingle((int)ReadBits(32));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadBool()
    {
        EnsureCapacity(1);
        int byteIdx = _bitOffset / 8;
        int bitInByteIdx = _bitOffset % 8;
        bool result = (_source[byteIdx] & (1 << (7 - bitInByteIdx))) != 0;
        _bitOffset++;
        return result;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ReadString()
    {
        int byteCount = (int)ReadVarInt();
        if (byteCount == 0) return string.Empty;

        int bitInByteIdx = _bitOffset % 8;
        if (bitInByteIdx == 0)
        {
            int byteOffset = _bitOffset / 8;
            EnsureCapacity(byteCount * 8);
            var result = Encoding.UTF8.GetString(_source.Slice(byteOffset, byteCount));
            _bitOffset += byteCount * 8;
            return result;
        }
        else
        {
            byte[] temp = ArrayPool<byte>.Shared.Rent(byteCount);
            try
            {
                for (int i = 0; i < byteCount; i++) temp[i] = (byte)ReadBits(8);
                return Encoding.UTF8.GetString(temp, 0, byteCount);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(temp);
            }
        }
    }
}
