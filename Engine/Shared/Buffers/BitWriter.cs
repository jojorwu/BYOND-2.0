using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Shared.Buffers;

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
    private void EnsureCapacity(int bitsNeeded)
    {
        int availableBits = _destination.Length * 8;
        if (_bitOffset + bitsNeeded > availableBits)
        {
            throw new IndexOutOfRangeException($"BitWriter overflow. BitOffset: {_bitOffset}, Requested: {bitsNeeded}, Total Capacity: {availableBits} bits ({_destination.Length} bytes)");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBits(ulong value, int bitCount)
    {
        if (bitCount == 0) return;
        EnsureCapacity(bitCount);

        while (bitCount > 0)
        {
            int byteIdx = _bitOffset / 8;
            int bitInByteIdx = _bitOffset % 8;
            int bitsToWriteInByte = Math.Min(bitCount, 8 - bitInByteIdx);

            ulong mask = (1UL << bitsToWriteInByte) - 1;
            ulong bits = (value >> (bitCount - bitsToWriteInByte)) & mask;

            if (bitInByteIdx == 0) _destination[byteIdx] = 0;

            // Clear the bits we are about to write to handle dirty pooled buffers
            byte clearMask = (byte)~(((1 << bitsToWriteInByte) - 1) << (8 - bitInByteIdx - bitsToWriteInByte));
            _destination[byteIdx] &= clearMask;
            _destination[byteIdx] |= (byte)(bits << (8 - bitInByteIdx - bitsToWriteInByte));

            _bitOffset += bitsToWriteInByte;
            bitCount -= bitsToWriteInByte;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PatchBits(int bitOffset, ulong value, int bitCount)
    {
        if (bitOffset < 0 || bitOffset + bitCount > _bitOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(bitOffset), "Cannot patch bits beyond current write offset or before 0.");
        }

        int savedOffset = _bitOffset;
        _bitOffset = bitOffset;

        while (bitCount > 0)
        {
            int byteIdx = _bitOffset / 8;
            int bitInByteIdx = _bitOffset % 8;
            int bitsToWriteInByte = Math.Min(bitCount, 8 - bitInByteIdx);

            ulong mask = (1UL << bitsToWriteInByte) - 1;
            ulong bits = (value >> (bitCount - bitsToWriteInByte)) & mask;

            byte clearMask = (byte)~(((1 << bitsToWriteInByte) - 1) << (8 - bitInByteIdx - bitsToWriteInByte));
            _destination[byteIdx] &= clearMask;
            _destination[byteIdx] |= (byte)(bits << (8 - bitInByteIdx - bitsToWriteInByte));

            _bitOffset += bitsToWriteInByte;
            bitCount -= bitsToWriteInByte;
        }

        _bitOffset = savedOffset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBool(bool value)
    {
        EnsureCapacity(1);
        int byteIdx = _bitOffset / 8;
        int bitInByteIdx = _bitOffset % 8;
        if (bitInByteIdx == 0) _destination[byteIdx] = 0;

        // Explicitly clear the bit first to handle dirty pooled buffers
        byte bitMask = (byte)(1 << (7 - bitInByteIdx));
        if (value) _destination[byteIdx] |= bitMask;
        else _destination[byteIdx] &= (byte)~bitMask;

        _bitOffset++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteByte(byte value)
    {
        WriteBits(value, 8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        int bitLen = bytes.Length * 8;
        EnsureCapacity(bitLen);

        int bitInByteIdx = _bitOffset % 8;
        if (bitInByteIdx == 0)
        {
            bytes.CopyTo(_destination.Slice(_bitOffset / 8));
            _bitOffset += bitLen;
        }
        else
        {
            for (int i = 0; i < bytes.Length; i++) WriteBits(bytes[i], 8);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteFloat(float value)
    {
        WriteBits((uint)BitConverter.SingleToInt32Bits(value), 32);
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
            return;
        }

        int byteCount = Encoding.UTF8.GetByteCount(s);
        WriteVarInt(byteCount);

        int bitInByteIdx = _bitOffset % 8;
        if (bitInByteIdx == 0)
        {
            int byteOffset = _bitOffset / 8;
            EnsureCapacity(byteCount * 8);
            Encoding.UTF8.GetBytes(s, _destination.Slice(byteOffset));
            _bitOffset += byteCount * 8;
        }
        else
        {
            // Use direct span access to Encoding.UTF8 to avoid byte[] allocation if possible
            // We still need a temporary span if we are not byte-aligned
            EnsureCapacity(byteCount * 8);

            if (byteCount <= 512)
            {
                Span<byte> temp = stackalloc byte[byteCount];
                Encoding.UTF8.GetBytes(s, temp);
                for (int i = 0; i < temp.Length; i++) WriteBits(temp[i], 8);
            }
            else
            {
                byte[] poolArray = ArrayPool<byte>.Shared.Rent(byteCount);
                try
                {
                    Encoding.UTF8.GetBytes(s, poolArray);
                    WriteBytes(poolArray.AsSpan(0, byteCount));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(poolArray);
                }
            }
        }
    }
}
