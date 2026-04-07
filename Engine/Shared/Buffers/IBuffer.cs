namespace Shared.Buffers;

/// <summary>
/// Defines a standard interface for buffer implementations within the engine.
/// Provides basic operations for memory lifecycle and tracking.
/// </summary>
public interface IBuffer
{
    /// <summary>
    /// Gets the total capacity of the buffer in bytes.
    /// </summary>
    int Capacity { get; }

    /// <summary>
    /// Gets the current write or read position within the buffer.
    /// </summary>
    int Position { get; }

    /// <summary>
    /// Resets the buffer to its initial state, typically clearing the position and potentially reclaiming memory.
    /// </summary>
    void Reset();
}
