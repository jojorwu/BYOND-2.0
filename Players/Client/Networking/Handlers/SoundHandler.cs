using System;
using System.Threading.Tasks;
using Shared;
using Shared.Interfaces;
using Shared.Services;
using Shared.Events;
using Shared.Messaging;
using Shared.Utils;
using Shared.Networking.Messages;

namespace Client.Networking.Handlers;

public class SoundHandler : BasePacketHandler
{
    private readonly IEventBus _eventBus;
    public override byte PacketTypeId => (byte)SnapshotMessageType.Sound;

    public SoundHandler(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public override Task HandleAsync(INetworkPeer peer, ReadOnlyMemory<byte> data)
    {
        var reader = new BitReader(data.Span);
        // Message Type
        reader.ReadBits(8);

        var msg = new SoundMessage();
        msg.Read(ref reader);

        _eventBus.Publish(new SoundEvent(msg.Data));
        return Task.CompletedTask;
    }
}
