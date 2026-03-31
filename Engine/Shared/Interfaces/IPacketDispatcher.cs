using System;
using System.Threading.Tasks;

namespace Shared.Interfaces;

public interface IPacketDispatcher
{
    void Initialize();
    void RegisterHandler(IPacketHandler handler);
    void UnregisterHandler(byte packetTypeId);
    void AddMiddleware(IPacketMiddleware middleware);
    Task DispatchAsync(INetworkPeer peer, string data);
    Task DispatchAsync(INetworkPeer peer, ReadOnlyMemory<byte> data);
}
