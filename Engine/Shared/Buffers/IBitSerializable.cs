namespace Shared.Buffers;

/// <summary>
/// Defines an object that can be serialized to a bit-stream.
/// </summary>
public interface IBitSerializable
{
    /// <summary>
    /// Writes the object's state to the specified bit-writer.
    /// </summary>
    void Write(ref BitWriter writer);

    /// <summary>
    /// Reads the object's state from the specified bit-reader.
    /// </summary>
    void Read(ref BitReader reader);
}

/// <summary>
/// Defines a contract for bit-stream operations.
/// Note: Concrete high-performance implementations should use <see cref="BitWriter"/> and <see cref="BitReader"/> ref structs.
/// </summary>
public interface IBitStreamInfo
{
    /// <summary>
    /// Gets the current bit position in the stream.
    /// </summary>
    long BitPosition { get; }

    /// <summary>
    /// Gets the total number of bits available or written.
    /// </summary>
    long BitLength { get; }
}
