using System;
using System.Threading.Tasks;
using Shared;
using Shared.Interfaces;
using Shared.Services;
using Shared.Events;
using Shared.Messaging;
using Shared.Utils;
using Core;

namespace Client.Networking.Handlers;

public class StopSoundHandler : BasePacketHandler
{
    private readonly IEventBus _eventBus;
    public override byte PacketTypeId => (byte)SnapshotMessageType.StopSound;

    public StopSoundHandler(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public override Task HandleAsync(INetworkPeer peer, ReadOnlyMemory<byte> data)
    {
        var reader = new BitReader(data.Span);
        // Message Type
        reader.ReadBits(8);

        var file = reader.ReadString();
        long? objectId = null;
        if (reader.ReadBool()) objectId = reader.ReadVarInt();

        _eventBus.Publish(new StopSoundEvent(file, objectId));
        return Task.CompletedTask;
    }
}
