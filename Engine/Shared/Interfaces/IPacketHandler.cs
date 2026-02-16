using System.Threading.Tasks;

namespace Shared.Interfaces
{
    /// <summary>
    /// Handles a specific type of network packet.
    /// </summary>
    public interface IPacketHandler
    {
        byte PacketTypeId { get; }
        Task HandleAsync(INetworkPeer peer, string data);
    }
}
