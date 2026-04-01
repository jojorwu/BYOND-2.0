using Shared.Interfaces;
using Shared.Utils;
using Shared.Networking;
using Shared.Networking.Messages;
using Shared.Messaging;
using Shared.Events;

namespace Shared.Networking.Handlers;

public class SoundMessageHandler : IMessageHandler
{
    private readonly IEventBus _eventBus;
    public byte MessageTypeId => (byte)SnapshotMessageType.Sound;

    public SoundMessageHandler(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public ValueTask HandleAsync(INetworkPeer peer, ReadOnlyMemory<byte> data)
    {
        var reader = new BitReader(data.Span);
        var msg = new SoundMessage();
        msg.Read(ref reader);

        _eventBus.Publish(new SoundEvent(msg.Data));
        return ValueTask.CompletedTask;
    }
}
