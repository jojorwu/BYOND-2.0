using Shared.Interfaces;
using Shared.Utils;
using Shared.Networking;
using Shared.Networking.Messages;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Networking.Handlers;

public class NetworkMessageHandler : IPacketHandler
{
    private readonly IServiceProvider _serviceProvider;

    public byte PacketTypeId => (byte)NetworkMessageType.Message;

    public NetworkMessageHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task HandleAsync(INetworkPeer peer, ReadOnlyMemory<byte> data)
    {
        var reader = new BitReader(data.Span);
        byte messageTypeId = reader.ReadByte();

        // We need a way to find specific message handlers
        // For now, let's just use a simple factory or service lookup
        var handlers = _serviceProvider.GetServices<IMessageHandler>();
        foreach(var handler in handlers)
        {
            if(handler.MessageTypeId == messageTypeId)
            {
                await handler.HandleAsync(peer, ref reader);
                return;
            }
        }
    }
}

public interface IMessageHandler
{
    byte MessageTypeId { get; }
    ValueTask HandleAsync(INetworkPeer peer, ref BitReader reader);
}
