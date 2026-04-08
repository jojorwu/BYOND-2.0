using System.Collections.Generic;

namespace Shared.Buffers;

using Shared.Interfaces;

/// <summary>
/// Defines a standard interface for buffer implementations within the engine.
/// Provides basic operations for memory lifecycle and tracking.
/// </summary>
public interface IBuffer : IShrinkable
{
    /// <summary>
    /// Returns diagnostic information about the buffer's current state.
    /// </summary>
    /// <returns>A dictionary containing metrics like allocation count, memory usage, etc.</returns>
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
    /// Gets the number of memory slabs currently allocated by the buffer.
    /// </summary>
    int SlabCount { get; }

    /// <summary>
    /// Gets the total number of bytes allocated across all slabs.
    /// </summary>
    long TotalAllocatedBytes { get; }

    /// <summary>
    /// Resets the buffer to its initial state, typically clearing the position and potentially reclaiming memory.
    /// </summary>
    void Reset();

    /// <summary>
    /// Returns a <see cref="System.ReadOnlySpan{T}"/> over a previously written segment.
    /// </summary>
    /// <param name="offset">The global offset of the segment.</param>
    /// <param name="length">The length of the segment.</param>
    /// <returns>A read-only span covering the requested segment.</returns>
    System.ReadOnlySpan<byte> GetSegmentAsSpan(long offset, int length);

    /// <summary>
    /// Returns a <see cref="System.Span{T}"/> over a previously written segment.
    /// </summary>
    /// <param name="offset">The global offset of the segment.</param>
    /// <param name="length">The length of the segment.</param>
    /// <returns>A mutable span covering the requested segment.</returns>
    System.Span<byte> GetMutableSegmentAsSpan(long offset, int length);

    /// <summary>
    /// Copies the written portion of the buffer to the specified stream.
    /// </summary>
    /// <param name="destination">The stream to copy to.</param>
    void CopyTo(System.IO.Stream destination);

    /// <summary>
    /// Copies the written portion of the buffer to the specified span.
    /// </summary>
    /// <param name="destination">The span to copy to.</param>
    void CopyTo(System.Span<byte> destination);
}
