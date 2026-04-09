using Shared.Interfaces;
using Shared.Utils;
using Shared.Networking;
using Shared.Networking.Messages;
using Microsoft.Extensions.DependencyInjection;

using Shared.Buffers;
namespace Shared.Networking.Handlers;

public class NetworkMessageHandler : IPacketHandler
{
    private readonly Dictionary<byte, IMessageHandler> _handlers = new();

    public byte PacketTypeId => (byte)NetworkMessageType.Message;

    public NetworkMessageHandler(IEnumerable<IMessageHandler> handlers)
    {
        foreach (var handler in handlers)
        {
            _handlers[handler.MessageTypeId] = handler;
        }
    }

    public async Task HandleAsync(INetworkPeer peer, ReadOnlyMemory<byte> data)
    {
        if (data.Length == 0) return;

        // We use a non-ref reader for the dispatching phase
        var reader = new BitReader(data.Span);
        byte messageTypeId = reader.ReadByte();

        if (_handlers.TryGetValue(messageTypeId, out var handler))
        {
            // Pass the remaining data to the specific handler
            var payload = data.Slice((int)(reader.BitsRead / 8));
            await handler.HandleAsync(peer, payload);
        }
    }
}

public interface IMessageHandler
{
    byte MessageTypeId { get; }
    ValueTask HandleAsync(INetworkPeer peer, ReadOnlyMemory<byte> data);
}
