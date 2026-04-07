using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
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
        long currentBitPos = _segmentBaseBitOffset + _bitOffset;
        if (currentBitPos + bitsNeeded > _totalBits)
        {
            throw new IndexOutOfRangeException($"BitReader overflow. BitOffset: {currentBitPos}, Requested: {bitsNeeded}, Total Capacity: {_totalBits} bits");
        }

        // Move to next segment if current is exhausted
        while (_bitOffset >= _source.Length * 8 && !_sequence.IsEmpty)
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

        ulong result = 0;
        while (bitCount > 0)
        {
            // Move to next segment if current bit offset is at the end of the current span
            if (_bitOffset >= _source.Length * 8 && !_sequence.IsEmpty)
            {
                _segmentBaseBitOffset += (long)_source.Length * 8;
                _bitOffset = 0;
                _sequence = _sequence.Slice(_source.Length);
                _source = _sequence.FirstSpan;
            }

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

    /// <summary>
    /// Advances the read position by the specified number of bits.
    /// </summary>
    /// <param name="bitCount">The number of bits to skip.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SkipBits(int bitCount)
    {
        _bitOffset += bitCount;
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
        int byteIdx = _bitOffset / 8;
        int bitInByteIdx = _bitOffset % 8;
        bool result = (_source[byteIdx] & (1 << (7 - bitInByteIdx))) != 0;
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
    /// Reads a signed 64-bit integer using variable-length encoding (LEB128-like).
    /// </summary>
    /// <returns>The read value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadVarInt()
    {
        // Fast path for byte-aligned VarInt
        if ((_bitOffset & 7) == 0)
        {
            long result = 0;
            int shift = 0;

            while (true)
            {
                EnsureCapacity(8);
                int byteIdx = _bitOffset / 8;
                byte b = _source[byteIdx];
                _bitOffset += 8;
                result |= (long)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return result;
                shift += 7;
                if (shift >= 64) throw new FormatException("VarInt too long");
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
        if (byteCount == 0) return string.Empty;

        int bitInByteIdx = _bitOffset % 8;
        if (bitInByteIdx == 0 && (_bitOffset / 8) + byteCount <= _source.Length)
        {
            int byteOffset = _bitOffset / 8;
            var result = Encoding.UTF8.GetString(_source.Slice(byteOffset, byteCount));
            _bitOffset += byteCount * 8;
            return result;
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
