using Shared.Interfaces;
using Shared.Utils;
using Shared.Networking;
using Shared.Networking.Messages;
using Shared.Messaging;
using Shared.Events;

namespace Shared.Networking.Handlers;

public class StopSoundMessageHandler : IMessageHandler
{
    private readonly IEventBus _eventBus;
    public byte MessageTypeId => (byte)SnapshotMessageType.StopSound;

    public StopSoundMessageHandler(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public ValueTask HandleAsync(INetworkPeer peer, ReadOnlyMemory<byte> data)
    {
        var reader = new BitReader(data.Span);
        var msg = new StopSoundMessage();
        msg.Read(ref reader);

        _eventBus.Publish(new StopSoundEvent(msg.File, msg.ObjectId));
        return ValueTask.CompletedTask;
    }
}
