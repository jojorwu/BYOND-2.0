using Shared.Interfaces;
using Shared.Utils;
using Shared.Networking;
using Shared.Networking.Messages;
using Shared.Config;

using Shared.Buffers;
namespace Shared.Networking.Handlers;

public class ClientCommandMessageHandler : IMessageHandler
{
    private readonly IConsoleCommandManager _commandManager;
    public byte MessageTypeId => (byte)ClientMessageType.Command;

    public ClientCommandMessageHandler(IConsoleCommandManager commandManager)
    {
        _commandManager = commandManager;
    }

    public async ValueTask HandleAsync(INetworkPeer peer, ReadOnlyMemory<byte> data)
    {
        var reader = new BitReader(data.Span);
        var msg = new ClientCommandMessage();
        msg.Read(ref reader);

        await _commandManager.ExecuteCommand(msg.Command);
    }
}
