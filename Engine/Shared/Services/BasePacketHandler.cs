using System;
using System.Threading.Tasks;
using Shared.Interfaces;

namespace Shared.Services;

public abstract class BasePacketHandler : IPacketHandler
{
    public abstract byte PacketTypeId { get; }

    public virtual Task HandleAsync(INetworkPeer peer, string data)
    {
        return Task.CompletedTask;
    }

    public virtual Task HandleAsync(INetworkPeer peer, ReadOnlyMemory<byte> data)
    {
        return HandleAsync(peer, System.Text.Encoding.UTF8.GetString(data.Span));
    }
}
