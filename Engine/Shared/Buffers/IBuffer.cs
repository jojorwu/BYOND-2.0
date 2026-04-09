using System.Collections.Generic;
using System.Buffers;
using Shared.Interfaces;

namespace Shared.Buffers;

/// <summary>
/// Provides diagnostic information and metadata about a buffer.
/// </summary>
public interface IBufferInfo
{
    /// <summary>
    /// Returns diagnostic information about the buffer's current state.
    /// </summary>
    IReadOnlyDictionary<string, object> GetDiagnosticInfo();

    /// <summary>
    /// Gets the total capacity of the buffer in bytes.
    /// </summary>
    long Capacity { get; }

    /// <summary>
    /// Gets the current write or read position within the buffer.
    /// </summary>
    long Position { get; }

    /// <summary>
    /// Gets the current length of data written to the buffer.
    /// </summary>
    long Length { get; }

    /// <summary>
    /// Gets the total number of bytes allocated for the buffer.
    /// </summary>
    long TotalAllocatedBytes { get; }

    /// <summary>
    /// Indicates whether the buffer utilizes pinned memory.
    /// </summary>
    bool IsPinned { get; }
}

/// <summary>
/// Defines a buffer that can be reset to its initial state.
/// </summary>
public interface IResettableBuffer
{
    /// <summary>
    /// Resets the buffer, clearing its content and potentially reclaiming memory.
    /// </summary>
    void Reset();
}

/// <summary>
/// Defines a buffer that supports read-only random access and sequence retrieval.
/// </summary>
public interface IReadableBuffer : IBufferInfo
{
    /// <summary>
    /// Gets the written portion of the buffer as a <see cref="ReadOnlySequence{byte}"/>.
    /// </summary>
    ReadOnlySequence<byte> WrittenSequence { get; }

    /// <summary>
    /// Returns a read-only span over a previously written segment.
    /// </summary>
    System.ReadOnlySpan<byte> GetSegmentAsSpan(long offset, int length);

    /// <summary>
    /// Copies the written portion of the buffer to the specified stream.
    /// </summary>
    void CopyTo(System.IO.Stream destination);

    /// <summary>
    /// Copies the written portion of the buffer to the specified span.
    /// </summary>
    void CopyTo(System.Span<byte> destination);
}

/// <summary>
/// Defines a buffer that supports mutable random access.
/// </summary>
public interface IMutableBuffer : IReadableBuffer
{
    /// <summary>
    /// Returns a mutable span over a previously written segment.
    /// </summary>
    System.Span<byte> GetMutableSegmentAsSpan(long offset, int length);
}

/// <summary>
/// Defines a standard interface for unified buffer implementations.
/// </summary>
public interface IBuffer : IMutableBuffer, IResettableBuffer, IShrinkable
{
    /// <summary>
    /// Gets the number of memory slabs currently allocated by the buffer.
    /// </summary>
    int SlabCount { get; }
}
