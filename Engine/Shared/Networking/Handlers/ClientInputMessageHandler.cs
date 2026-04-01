using Shared.Interfaces;
using Shared.Utils;
using Shared.Networking;
using Shared.Networking.Messages;
using Microsoft.Extensions.Logging;

namespace Shared.Networking.Handlers;

public class ClientInputMessageHandler : IMessageHandler
{
    private readonly ILogger<ClientInputMessageHandler> _logger;
    public byte MessageTypeId => (byte)ClientMessageType.Input;

    public ClientInputMessageHandler(ILogger<ClientInputMessageHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask HandleAsync(INetworkPeer peer, ReadOnlyMemory<byte> data)
    {
        var reader = new BitReader(data.Span);
        var msg = new ClientInputMessage();
        msg.Read(ref reader);

        _logger.LogDebug("Received input from {Peer}: {Type} ({X}, {Y})", peer.Nickname, msg.InputType, msg.X, msg.Y);

        return ValueTask.CompletedTask;
    }
}
