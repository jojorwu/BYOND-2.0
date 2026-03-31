using Shared.Interfaces;
using Shared.Utils;
using Shared.Networking;
using Shared.Networking.Messages;
using Shared.Config;

namespace Shared.Networking.Handlers;

public class ClientCommandMessageHandler : IMessageHandler
{
    private readonly IConsoleCommandManager _commandManager;
    public byte MessageTypeId => (byte)ClientMessageType.Command;

    public ClientCommandMessageHandler(IConsoleCommandManager commandManager)
    {
        _commandManager = commandManager;
    }

    public ValueTask HandleAsync(INetworkPeer peer, ref BitReader reader)
    {
        var msg = new ClientCommandMessage();
        msg.Read(ref reader);

        _ = _commandManager.ExecuteCommand(msg.Command);
        return ValueTask.CompletedTask;
    }
}
