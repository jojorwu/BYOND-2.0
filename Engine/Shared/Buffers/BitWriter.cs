using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Shared.Buffers;

/// <summary>
/// A high-performance bit-level writer for compact serialization.
/// </summary>
public ref struct BitWriter
{
    private Span<byte> _destination;
    private int _bitOffset;

    /// <summary>
    /// Initializes a new instance of the <see cref="BitWriter"/> struct with a destination span.
    /// </summary>
    /// <param name="destination">The span to write bits into.</param>
    public BitWriter(Span<byte> destination)
    {
        _destination = destination;
        _bitOffset = 0;
    }

    /// <summary>
    /// Gets the total number of bits written to the buffer.
    /// </summary>
    public int BitsWritten => _bitOffset;

    /// <summary>
    /// Gets the total number of bytes written to the buffer, rounded up.
    /// </summary>
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

    /// <summary>
    /// Writes the specified number of bits from an unsigned 64-bit value to the buffer.
    /// </summary>
    /// <param name="value">The value containing the bits to write.</param>
    /// <param name="bitCount">The number of bits to write (up to 64).</param>
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

    /// <summary>
    /// Overwrites a previously written sequence of bits at a specific offset.
    /// </summary>
    /// <param name="bitOffset">The bit offset where patching should begin.</param>
    /// <param name="value">The new value containing the bits.</param>
    /// <param name="bitCount">The number of bits to overwrite.</param>
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

    /// <summary>
    /// Writes a single boolean value as one bit.
    /// </summary>
    /// <param name="value">The boolean value to write.</param>
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

    /// <summary>
    /// Writes a single byte (8 bits) to the buffer.
    /// </summary>
    /// <param name="value">The byte value to write.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteByte(byte value)
    {
        WriteBits(value, 8);
    }

    /// <summary>
    /// Writes a sequence of bytes to the buffer.
    /// Optimizes for byte-aligned writes using bulk copying.
    /// </summary>
    /// <param name="bytes">The span of bytes to write.</param>
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

    /// <summary>
    /// Writes a 32-bit floating-point value in Big-Endian format.
    /// </summary>
    /// <param name="value">The float value to write.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteFloat(float value)
    {
        if ((_bitOffset & 7) == 0)
        {
            EnsureCapacity(32);
            BinaryPrimitives.WriteSingleBigEndian(_destination.Slice(_bitOffset / 8), value);
            _bitOffset += 32;
        }
        else
        {
            WriteBits((uint)BitConverter.SingleToInt32Bits(value), 32);
        }
    }

    /// <summary>
    /// Writes a signed 32-bit integer using the specified number of bits.
    /// </summary>
    /// <param name="value">The integer value to write.</param>
    /// <param name="bitCount">The number of bits to use.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt(int value, int bitCount)
    {
        if (bitCount == 32 && (_bitOffset & 7) == 0)
        {
            EnsureCapacity(32);
            BinaryPrimitives.WriteInt32BigEndian(_destination.Slice(_bitOffset / 8), value);
            _bitOffset += 32;
        }
        else
        {
            WriteBits((ulong)value, bitCount);
        }
    }

    /// <summary>
    /// Writes a signed 64-bit integer using the specified number of bits.
    /// </summary>
    /// <param name="value">The long value to write.</param>
    /// <param name="bitCount">The number of bits to use.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLong(long value, int bitCount)
    {
        if (bitCount == 64 && (_bitOffset & 7) == 0)
        {
            EnsureCapacity(64);
            BinaryPrimitives.WriteInt64BigEndian(_destination.Slice(_bitOffset / 8), value);
            _bitOffset += 64;
        }
        else
        {
            WriteBits((ulong)value, bitCount);
        }
    }

    /// <summary>
    /// Writes a 64-bit floating-point value in Big-Endian format.
    /// </summary>
    /// <param name="value">The double value to write.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDouble(double value)
    {
        if ((_bitOffset & 7) == 0)
        {
            EnsureCapacity(64);
            BinaryPrimitives.WriteDoubleBigEndian(_destination.Slice(_bitOffset / 8), value);
            _bitOffset += 64;
        }
        else
        {
            WriteBits(BitConverter.DoubleToUInt64Bits(value), 64);
        }
    }

    /// <summary>
    /// Attempts to write the specified number of bits. Returns false if there is not enough capacity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryWriteBits(ulong value, int bitCount)
    {
        if (_bitOffset + bitCount > _destination.Length * 8) return false;
        WriteBits(value, bitCount);
        return true;
    }

    /// <summary>
    /// Writes a signed 64-bit integer using a variable-length encoding (LEB128-like).
    /// </summary>
    /// <param name="value">The value to write.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteVarInt(long value)
    {
        ulong v = (ulong)value;

        // Fast path for byte-aligned VarInt
        if ((_bitOffset & 7) == 0)
        {
            int byteIdx = _bitOffset / 8;
            while (v >= 0x80)
            {
                if (byteIdx >= _destination.Length) throw new IndexOutOfRangeException("BitWriter overflow during VarInt");
                _destination[byteIdx++] = (byte)((v & 0x7F) | 0x80);
                v >>= 7;
                _bitOffset += 8;
            }
            if (byteIdx >= _destination.Length) throw new IndexOutOfRangeException("BitWriter overflow during VarInt");
            _destination[byteIdx] = (byte)(v & 0x7F);
            _bitOffset += 8;
            return;
        }

        while (v >= 0x80)
        {
            WriteBool(true);
            WriteBits(v & 0x7F, 7);
            v >>= 7;
        }
        WriteBool(false);
        WriteBits(v & 0x7F, 7);
    }

    /// <summary>
    /// Writes a signed 64-bit integer using ZigZag encoding to handle negative values efficiently with VarInt.
    /// </summary>
    /// <param name="value">The value to write.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteZigZag(long value)
    {
        ulong zigzag = (ulong)((value << 1) ^ (value >> 63));
        WriteVarInt((long)zigzag);
    }

    /// <summary>
    /// Writes a UTF-8 encoded string to the buffer, prefixed by its byte length as a VarInt.
    /// </summary>
    /// <param name="s">The string to write.</param>
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
