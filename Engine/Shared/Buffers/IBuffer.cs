using System.Collections.Generic;

namespace Shared.Buffers;

/// <summary>
/// Defines a standard interface for buffer implementations within the engine.
/// Provides basic operations for memory lifecycle and tracking.
/// </summary>
public interface IBuffer
{
    /// <summary>
    /// Returns diagnostic information about the buffer's current state.
    /// </summary>
    /// <returns>A dictionary containing metrics like allocation count, memory usage, etc.</returns>
    IReadOnlyDictionary<string, object> GetDiagnosticInfo();

    /// <summary>
    /// Gets the total capacity of the buffer in bytes.
    /// </summary>
    int Capacity { get; }

    /// <summary>
    /// Gets the current write or read position within the buffer.
    /// </summary>
    int Position { get; }

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
}
