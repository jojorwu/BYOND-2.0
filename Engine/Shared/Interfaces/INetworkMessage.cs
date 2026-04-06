using Shared.Utils;

using Shared.Buffers;
namespace Shared.Interfaces;

/// <summary>
/// Represents a structured message that can be sent over the network.
/// </summary>
public interface INetworkMessage
{
    /// <summary>
    /// The unique identifier for this message type.
    /// </summary>
    byte MessageTypeId { get; }

    /// <summary>
    /// Serializes the message to a bit stream.
    /// </summary>
    void Write(ref BitWriter writer);

    /// <summary>
    /// Deserializes the message from a bit stream.
    /// </summary>
    void Read(ref BitReader reader);
}
