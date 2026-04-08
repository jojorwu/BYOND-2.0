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
    private readonly IBufferWriter<byte>? _writer;
    private long _globalBaseBitOffset;

    /// <summary>
    /// Initializes a new instance of the <see cref="BitWriter"/> struct with a destination span.
    /// </summary>
    /// <param name="destination">The span to write bits into.</param>
    public BitWriter(Span<byte> destination)
    {
        _destination = destination;
        _bitOffset = 0;
        _writer = null;
        _globalBaseBitOffset = 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BitWriter"/> struct with a buffer writer.
    /// </summary>
    /// <param name="writer">The buffer writer to write bits into.</param>
    public BitWriter(IBufferWriter<byte> writer)
    {
        _writer = writer;
        _destination = writer.GetSpan();
        _bitOffset = 0;
        _globalBaseBitOffset = 0;
    }

    /// <summary>
    /// Gets the total number of bits written to the buffer.
    /// </summary>
    public long BitsWritten => _globalBaseBitOffset + _bitOffset;

    /// <summary>
    /// Gets the total number of bytes written to the buffer, rounded up.
    /// </summary>
    public long BytesWritten => (BitsWritten + 7) / 8;

    /// <summary>
    /// Flushes any pending written data to the underlying writer and advances its position.
    /// </summary>
    public void Flush()
    {
        if (_writer != null)
        {
            if (_bitOffset > 0)
            {
                int bytesToAdvance = (_bitOffset + 7) / 8;
                _writer.Advance(bytesToAdvance);
                _globalBaseBitOffset += bytesToAdvance * 8;
                _bitOffset = 0;
            }
            _destination = Span<byte>.Empty;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int bitsNeeded)
    {
        int availableBits = (_destination.Length * 8) - _bitOffset;
        if (bitsNeeded > availableBits)
        {
            if (_writer != null)
            {
                Flush();
                _destination = _writer.GetSpan((bitsNeeded + 7) / 8);
                availableBits = _destination.Length * 8;
                if (bitsNeeded > availableBits)
                {
                     throw new IndexOutOfRangeException($"BitWriter overflow. Even after flush and GetSpan, requested bits ({bitsNeeded}) exceeds available segment capacity ({availableBits} bits).");
                }
            }
            else
            {
                throw new IndexOutOfRangeException($"BitWriter overflow. BitPositionInSegment: {_bitOffset}, RequestedBits: {bitsNeeded}, CurrentSegmentCapacityBits: {_destination.Length * 8}");
            }
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

        // Fast path for aligned multi-bit writes (e.g. 8, 16, 32, 64 bits)
        if ((_bitOffset & 7) == 0)
        {
            int byteIdx = _bitOffset >> 3;
            if (bitCount == 8)
            {
                _destination[byteIdx] = (byte)value;
                _bitOffset += 8;
                return;
            }
            if (bitCount == 16)
            {
                BinaryPrimitives.WriteUInt16BigEndian(_destination.Slice(byteIdx), (ushort)value);
                _bitOffset += 16;
                return;
            }
            if (bitCount == 32)
            {
                BinaryPrimitives.WriteUInt32BigEndian(_destination.Slice(byteIdx), (uint)value);
                _bitOffset += 32;
                return;
            }
            if (bitCount == 64)
            {
                BinaryPrimitives.WriteUInt64BigEndian(_destination.Slice(byteIdx), value);
                _bitOffset += 64;
                return;
            }
        }

        while (bitCount > 0)
        {
            int byteIdx = _bitOffset >> 3;
            int bitInByteIdx = _bitOffset & 7;
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
    /// Supports cross-segment patching if the underlying writer is an <see cref="IBuffer"/>.
    /// </summary>
    /// <param name="bitOffset">The bit offset where patching should begin.</param>
    /// <param name="value">The new value containing the bits.</param>
    /// <param name="bitCount">The number of bits to overwrite.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PatchBits(long bitOffset, ulong value, int bitCount)
    {
        if (bitOffset + bitCount > BitsWritten || bitOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bitOffset), "Cannot patch bits beyond current write position.");
        }

        // Fast path: patching within the current segment
        if (bitOffset >= _globalBaseBitOffset)
        {
            int savedOffset = _bitOffset;
            _bitOffset = (int)(bitOffset - _globalBaseBitOffset);

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
            return;
        }

        // Slow path: cross-segment patching using IBuffer random access
        if (_writer is IBuffer buffer)
        {
            long currentBit = bitOffset;
            while (bitCount > 0)
            {
                long byteOffset = currentBit / 8;
                int bitInByteIdx = (int)(currentBit % 8);
                int bitsToWriteInByte = Math.Min(bitCount, 8 - bitInByteIdx);

                // Acquire the span for exactly one byte to ensure we don't cross slab boundaries blindly
                var targetSpan = buffer.GetMutableSegmentAsSpan(byteOffset, 1);
                byte byteVal = targetSpan[0];

                ulong mask = (1UL << bitsToWriteInByte) - 1;
                ulong bits = (value >> (bitCount - bitsToWriteInByte)) & mask;

                byte clearMask = (byte)~(((1 << bitsToWriteInByte) - 1) << (8 - bitInByteIdx - bitsToWriteInByte));
                byteVal &= clearMask;
                byteVal |= (byte)(bits << (8 - bitInByteIdx - bitsToWriteInByte));

                targetSpan[0] = byteVal;

                currentBit += bitsToWriteInByte;
                bitCount -= bitsToWriteInByte;
            }
        }
        else
        {
            throw new NotSupportedException("Cross-segment patching is only supported when the underlying writer implements IBuffer.");
        }
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
    /// Writes the specified number of bits from a bit reader to the buffer.
    /// </summary>
    /// <param name="reader">The reader containing the bits to write.</param>
    /// <param name="bitCount">The number of bits to transfer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBits(ref BitReader reader, int bitCount)
    {
        while (bitCount >= 8)
        {
            WriteByte(reader.ReadByte());
            bitCount -= 8;
        }
        if (bitCount > 0)
        {
            WriteBits(reader.ReadBits(bitCount), bitCount);
        }
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

        EnsureCapacity(byteCount << 3);

        // Fast path for aligned writes
        if ((_bitOffset & 7) == 0)
        {
            int byteIdx = _bitOffset >> 3;
            Encoding.UTF8.GetBytes(s, _destination.Slice(byteIdx));
            _bitOffset += byteCount << 3;
        }
        else
        {
            // Unaligned write: we use a stack-allocated or pooled buffer and write bit-by-bit
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
