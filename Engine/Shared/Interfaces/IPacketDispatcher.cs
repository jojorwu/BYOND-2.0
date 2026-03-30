using System;
using System.Threading.Tasks;

namespace Shared.Interfaces;

public interface IPacketDispatcher
{
    void RegisterHandler(IPacketHandler handler);
    void UnregisterHandler(byte packetTypeId);
    void AddMiddleware(IPacketMiddleware middleware);
    Task DispatchAsync(INetworkPeer peer, string data);
    Task DispatchAsync(INetworkPeer peer, ReadOnlyMemory<byte> data);
    void Dispatch(INetworkPeer peer, byte typeId, ReadOnlySpan<byte> payload);
}
