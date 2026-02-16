namespace Shared.Interfaces
{
    /// <summary>
    /// Represents a data packet in the network protocol.
    /// </summary>
    public interface IPacket
    {
        byte TypeId { get; }
        void Deserialize(string data);
        string Serialize();
    }
}
