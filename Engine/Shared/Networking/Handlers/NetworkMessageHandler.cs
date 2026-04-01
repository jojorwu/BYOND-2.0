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
        // We use a non-ref reader for the dispatching phase
        var reader = new BitReader(data.Span);
        byte messageTypeId = reader.ReadByte();

        // Pass the remaining data to the specific handler
        var payload = data.Slice(reader.BitsRead / 8);

        var handlers = _serviceProvider.GetServices<IMessageHandler>();
        foreach(var handler in handlers)
        {
            if(handler.MessageTypeId == messageTypeId)
            {
                await handler.HandleAsync(peer, payload);
                return;
            }
        }
    }
}

public interface IMessageHandler
{
    byte MessageTypeId { get; }
    ValueTask HandleAsync(INetworkPeer peer, ReadOnlyMemory<byte> data);
}
