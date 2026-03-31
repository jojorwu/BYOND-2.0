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

    public ValueTask HandleAsync(INetworkPeer peer, ref BitReader reader)
    {
        var msg = new ClientInputMessage();
        msg.Read(ref reader);

        // In a real implementation, this would be passed to the movement system or entity controller
        _logger.LogDebug("Received input from {Peer}: {Type} ({X}, {Y})", peer.Nickname, msg.InputType, msg.X, msg.Y);

        return ValueTask.CompletedTask;
    }
}
