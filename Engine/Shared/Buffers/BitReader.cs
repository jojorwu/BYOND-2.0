using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.InteropServices;
using System.Text;

namespace Shared.Buffers;

/// <summary>
/// A high-performance bit-level reader for compact serialization.
/// Supports both single-span and multi-segment sources via <see cref="ReadOnlySequence{T}"/>.
/// </summary>
public ref struct BitReader
{
    private ReadOnlySpan<byte> _source;
    private int _bitOffset;
    private ReadOnlySequence<byte> _sequence;
    private long _totalBits;
    private long _segmentBaseBitOffset;

    /// <summary>
    /// Initializes a new instance of the <see cref="BitReader"/> struct with multiple memory segments.
    /// </summary>
    /// <param name="segments">A provider of memory segments to read from.</param>
    public BitReader(SnapshotBuffer.SegmentProvider segments)
    {
        if (segments.Count == 0)
        {
            _source = ReadOnlySpan<byte>.Empty;
            _sequence = default;
            _totalBits = 0;
        }
        else if (segments.Count == 1)
        {
            _source = segments[0].Span;
            _sequence = default;
            _totalBits = (long)_source.Length * 8;
        }
        else
        {
            var builder = new SequenceBuilder();
            foreach (var segment in segments) builder.Add(segment);
            _sequence = builder.Build();
            _source = _sequence.FirstSpan;
            _totalBits = _sequence.Length * 8;
        }

        _bitOffset = 0;
        _segmentBaseBitOffset = 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BitReader"/> struct with multiple memory segments.
    /// </summary>
    /// <param name="segments">A span of memory segments to read from.</param>
    public BitReader(ReadOnlySpan<ReadOnlyMemory<byte>> segments)
    {
        if (segments.Length == 0)
        {
            _source = ReadOnlySpan<byte>.Empty;
            _sequence = default;
            _totalBits = 0;
        }
        else if (segments.Length == 1)
        {
            _source = segments[0].Span;
            _sequence = default;
            _totalBits = (long)_source.Length * 8;
        }
        else
        {
            var builder = new SequenceBuilder();
            foreach (var segment in segments) builder.Add(segment);
            _sequence = builder.Build();
            _source = _sequence.FirstSpan;
            _totalBits = _sequence.Length * 8;
        }

        _bitOffset = 0;
        _segmentBaseBitOffset = 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BitReader"/> struct with a source span.
    /// </summary>
    /// <param name="source">The read-only span to read bits from.</param>
    public BitReader(ReadOnlySpan<byte> source)
    {
        _source = source;
        _bitOffset = 0;
        _sequence = default;
        _totalBits = (long)source.Length * 8;
        _segmentBaseBitOffset = 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BitReader"/> struct with a sequence.
    /// </summary>
    /// <param name="sequence">The sequence to read from.</param>
    public BitReader(ReadOnlySequence<byte> sequence)
    {
        _sequence = sequence;
        _source = sequence.FirstSpan;
        _bitOffset = 0;
        _totalBits = sequence.Length * 8;
        _segmentBaseBitOffset = 0;
    }

    /// <summary>
    /// Gets the total number of bits read from the buffer.
    /// </summary>
    public long BitsRead => _segmentBaseBitOffset + _bitOffset;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int bitsNeeded)
    {
        if (BitsRead + bitsNeeded > _totalBits)
        {
            throw new IndexOutOfRangeException($"BitReader overflow. BitPosition: {BitsRead}, RequestedBits: {bitsNeeded}, TotalCapacityBits: {_totalBits}");
        }

        AdvanceSegmentIfNeeded();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AdvanceSegmentIfNeeded()
    {
        while (_bitOffset >= (long)_source.Length * 8 && !_sequence.IsEmpty)
        {
            _segmentBaseBitOffset += (long)_source.Length * 8;
            _bitOffset -= _source.Length * 8;
            _sequence = _sequence.Slice(_source.Length);
            _source = _sequence.FirstSpan;
        }
    }

    /// <summary>
    /// Reads the specified number of bits and returns them as an unsigned 64-bit integer.
    /// </summary>
    /// <param name="bitCount">The number of bits to read (up to 64).</param>
    /// <returns>An unsigned 64-bit integer containing the read bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadBits(int bitCount)
    {
        if (bitCount == 0) return 0;
        EnsureCapacity(bitCount);

        ref byte sourceRef = ref MemoryMarshal.GetReference(_source);

        // Fast path for aligned multi-bit reads within current segment
        if ((_bitOffset & 7) == 0 && _bitOffset + bitCount <= (long)_source.Length * 8)
        {
            int byteIdx = _bitOffset >> 3;
            if (bitCount == 8)
            {
                ulong val = Unsafe.Add(ref sourceRef, byteIdx);
                _bitOffset += 8;
                return val;
            }
            if (bitCount == 16)
            {
                ulong val = BinaryPrimitives.ReadUInt16BigEndian(_source.Slice(byteIdx));
                _bitOffset += 16;
                return val;
            }
            if (bitCount == 32)
            {
                ulong val = BinaryPrimitives.ReadUInt32BigEndian(_source.Slice(byteIdx));
                _bitOffset += 32;
                return val;
            }
            if (bitCount == 64)
            {
                ulong val = BinaryPrimitives.ReadUInt64BigEndian(_source.Slice(byteIdx));
                _bitOffset += 64;
                return val;
            }
        }

        ulong result = 0;
        while (bitCount > 0)
        {
            if (_bitOffset >= (long)_source.Length * 8)
            {
                AdvanceSegmentIfNeeded();
                sourceRef = ref MemoryMarshal.GetReference(_source);
            }

            int byteIdx = (int)(_bitOffset >> 3);
            int bitOffsetInByte = _bitOffset & 7;
            int bitsAvailableInByte = 8 - bitOffsetInByte;
            int bitsToRead = Math.Min(bitCount, bitsAvailableInByte);

            ulong mask = (1UL << bitsToRead) - 1;
            ulong bits = (ulong)(Unsafe.Add(ref sourceRef, byteIdx) >> (bitsAvailableInByte - bitsToRead)) & mask;

            result = (result << bitsToRead) | bits;

            _bitOffset += bitsToRead;
            bitCount -= bitsToRead;
        }
        return result;
    }

    /// <summary>
    /// Advances the read position by the specified number of bits.
    /// </summary>
    /// <param name="bitCount">The number of bits to skip.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SkipBits(int bitCount)
    {
        if (BitsRead + bitCount > _totalBits)
        {
            throw new IndexOutOfRangeException("BitReader skip overflow.");
        }

        _bitOffset += bitCount;
        if (_bitOffset >= (long)_source.Length * 8)
        {
            AdvanceSegmentIfNeeded();
        }
    }

    /// <summary>
    /// Reads a single byte (8 bits) from the buffer.
    /// </summary>
    /// <returns>The read byte.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadByte()
    {
        return (byte)ReadBits(8);
    }

    /// <summary>
    /// Reads a sequence of bytes from the buffer.
    /// Optimizes for byte-aligned reads using bulk copying.
    /// </summary>
    /// <param name="destination">The span to read bytes into.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadBytes(Span<byte> destination)
    {
        int bitLen = destination.Length * 8;
        EnsureCapacity(bitLen);

        // Fast path for aligned reads that fit within the current segment
        if ((_bitOffset & 7) == 0 && (_bitOffset / 8) + destination.Length <= _source.Length)
        {
            _source.Slice(_bitOffset / 8, destination.Length).CopyTo(destination);
            _bitOffset += bitLen;
        }
        else
        {
            for (int i = 0; i < destination.Length; i++) destination[i] = ReadByte();
        }
    }

    /// <summary>
    /// Reads a 32-bit floating-point value in Big-Endian format.
    /// </summary>
    /// <returns>The read float value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadFloat()
    {
        if ((_bitOffset & 7) == 0 && (_bitOffset / 8) + 4 <= _source.Length)
        {
            float result = BinaryPrimitives.ReadSingleBigEndian(_source.Slice(_bitOffset / 8));
            _bitOffset += 32;
            return result;
        }
        return BitConverter.Int32BitsToSingle((int)ReadBits(32));
    }

    /// <summary>
    /// Reads a single boolean value from one bit.
    /// </summary>
    /// <returns>True if the bit is set; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadBool()
    {
        EnsureCapacity(1);
        int byteIdx = _bitOffset >> 3;
        int bitInByteIdx = _bitOffset & 7;

        bool result = (Unsafe.Add(ref MemoryMarshal.GetReference(_source), byteIdx) & (1 << (7 - bitInByteIdx))) != 0;
        _bitOffset++;
        return result;
    }

    /// <summary>
    /// Reads a signed 32-bit integer using the specified number of bits.
    /// </summary>
    /// <param name="bitCount">The number of bits to read.</param>
    /// <returns>The read integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt(int bitCount)
    {
        if (bitCount == 32 && (_bitOffset & 7) == 0 && (_bitOffset / 8) + 4 <= _source.Length)
        {
            int result = BinaryPrimitives.ReadInt32BigEndian(_source.Slice(_bitOffset / 8));
            _bitOffset += 32;
            return result;
        }
        return (int)ReadBits(bitCount);
    }

    /// <summary>
    /// Reads a signed 64-bit integer using the specified number of bits.
    /// </summary>
    /// <param name="bitCount">The number of bits to read.</param>
    /// <returns>The read long integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadLong(int bitCount)
    {
        if (bitCount == 64 && (_bitOffset & 7) == 0 && (_bitOffset / 8) + 8 <= _source.Length)
        {
            long result = BinaryPrimitives.ReadInt64BigEndian(_source.Slice(_bitOffset / 8));
            _bitOffset += 64;
            return result;
        }
        return (long)ReadBits(bitCount);
    }

    /// <summary>
    /// Reads a 64-bit floating-point value in Big-Endian format.
    /// </summary>
    /// <returns>The read double value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadDouble()
    {
        if ((_bitOffset & 7) == 0 && (_bitOffset / 8) + 8 <= _source.Length)
        {
            double result = BinaryPrimitives.ReadDoubleBigEndian(_source.Slice(_bitOffset / 8));
            _bitOffset += 64;
            return result;
        }
        return BitConverter.UInt64BitsToDouble(ReadBits(64));
    }

    /// <summary>
    /// Attempts to read the specified number of bits. Returns false if there is not enough data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadBits(int bitCount, out ulong result)
    {
        if (BitsRead + bitCount > _totalBits)
        {
            result = 0;
            return false;
        }
        result = ReadBits(bitCount);
        return true;
    }

    /// <summary>
    /// Attempts to read a single boolean value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadBool(out bool result)
    {
        if (BitsRead + 1 > _totalBits)
        {
            result = false;
            return false;
        }
        result = ReadBool();
        return true;
    }

    /// <summary>
    /// Attempts to read a 32-bit integer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadInt(int bitCount, out int result)
    {
        if (BitsRead + bitCount > _totalBits)
        {
            result = 0;
            return false;
        }
        result = ReadInt(bitCount);
        return true;
    }

    /// <summary>
    /// Attempts to read a VarInt.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadVarInt(out long result)
    {
        if (BitsRead + 8 > _totalBits)
        {
            result = 0;
            return false;
        }

        try
        {
            result = ReadVarInt();
            return true;
        }
        catch
        {
            result = 0;
            return false;
        }
    }

    /// <summary>
    /// Attempts to read a UTF-8 encoded string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadString(out string? result)
    {
        if (!TryReadVarInt(out long byteCount))
        {
            result = null;
            return false;
        }

        if (BitsRead + (byteCount << 3) > _totalBits)
        {
            result = null;
            return false;
        }

        result = ReadStringInternal((int)byteCount);
        return true;
    }

    private string ReadStringInternal(int byteCount)
    {
        if (byteCount == 0) return string.Empty;

        // Fast path for aligned reads
        if ((_bitOffset & 7) == 0 && (_bitOffset >> 3) + byteCount <= _source.Length)
        {
            int byteOffset = _bitOffset >> 3;
            var result = Encoding.UTF8.GetString(_source.Slice(byteOffset, byteCount));
            _bitOffset += byteCount << 3;
            return result;
        }
        else
        {
            // Unaligned or cross-segment read
            if (byteCount <= 512)
            {
                Span<byte> temp = stackalloc byte[byteCount];
                for (int i = 0; i < temp.Length; i++) temp[i] = ReadByte();
                return Encoding.UTF8.GetString(temp);
            }
            else
            {
                byte[] temp = ArrayPool<byte>.Shared.Rent(byteCount);
                try
                {
                    ReadBytes(temp.AsSpan(0, byteCount));
                    return Encoding.UTF8.GetString(temp, 0, byteCount);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(temp);
                }
            }
        }
    }

    /// <summary>
    /// Reads a signed 64-bit integer using variable-length encoding (LEB128-like).
    /// </summary>
    /// <returns>The read value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadVarInt()
    {
        // Fast path for byte-aligned VarInt
        if ((_bitOffset & 7) == 0)
        {
            // SIMD-accelerated boundary detection for aligned VarInt
            if (Sse2.IsSupported && (_bitOffset >> 3) + 16 <= _source.Length)
            {
                Vector128<byte> data = Vector128.Create(_source.Slice(_bitOffset >> 3, 16));
                uint mask = (uint)Sse2.MoveMask(data);
                if (mask != 0xFFFF)
                {
                    // Found at least one byte with MSB=0 (boundary)
                    int boundaryIdx = BitOperations.TrailingZeroCount(mask ^ 0xFFFF);
                    // Standard byte-by-byte decode for the found bytes
                    long result = 0;
                    int shift = 0;
                    for (int i = 0; i <= boundaryIdx; i++)
                    {
                        byte b = _source[(_bitOffset >> 3) + i];
                        result |= (long)(b & 0x7F) << shift;
                        shift += 7;
                    }
                    _bitOffset += (boundaryIdx + 1) << 3;
                    return result;
                }
            }

            {
                long result = 0;
                int shift = 0;
                while (true)
                {
                    EnsureCapacity(8);
                    int byteIdx = _bitOffset >> 3;
                    byte b = _source[byteIdx];
                    _bitOffset += 8;
                    result |= (long)(b & 0x7F) << shift;
                    if ((b & 0x80) == 0) return result;
                    shift += 7;
                    if (shift >= 64) throw new FormatException("VarInt too long");
                }
            }
        }

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
    }

    /// <summary>
    /// Reads a signed 64-bit integer using ZigZag and VarInt encoding.
    /// </summary>
    /// <returns>The read value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadZigZag()
    {
        ulong zigzag = (ulong)ReadVarInt();
        return (long)(zigzag >> 1) ^ -(long)(zigzag & 1);
    }

    /// <summary>
    /// Reads a UTF-8 encoded string, prefixed by its byte length as a VarInt.
    /// </summary>
    /// <returns>The read string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ReadString()
    {
        int byteCount = (int)ReadVarInt();
        return ReadStringInternal(byteCount);
    }
}
