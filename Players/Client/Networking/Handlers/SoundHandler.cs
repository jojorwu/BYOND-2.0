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

        var sound = new SoundData();
        sound.File = reader.ReadString();
        sound.Volume = (float)reader.ReadDouble();
        sound.Pitch = (float)reader.ReadDouble();
        sound.Repeat = reader.ReadBool();

        if (reader.ReadBool()) sound.X = reader.ReadZigZag();
        if (reader.ReadBool()) sound.Y = reader.ReadZigZag();
        if (reader.ReadBool()) sound.Z = reader.ReadZigZag();
        if (reader.ReadBool()) sound.ObjectId = reader.ReadVarInt();

        sound.Falloff = (float)reader.ReadDouble();

        _eventBus.Publish(new SoundEvent(sound));
        return Task.CompletedTask;
    }
}
