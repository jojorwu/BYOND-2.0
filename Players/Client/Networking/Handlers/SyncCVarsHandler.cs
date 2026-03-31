using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Shared;
using Shared.Interfaces;
using Shared.Services;
using Shared.Events;
using Shared.Messaging;
using Shared.Utils;
using Shared.Networking.Messages;

namespace Client.Networking.Handlers;

public class SyncCVarsHandler : BasePacketHandler
{
    private readonly IEventBus _eventBus;
    public override byte PacketTypeId => (byte)SnapshotMessageType.SyncCVars;

    public SyncCVarsHandler(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public override Task HandleAsync(INetworkPeer peer, ReadOnlyMemory<byte> data)
    {
        var reader = new BitReader(data.Span);
        // Message Type
        reader.ReadBits(8);

        var msg = new CVarSyncMessage();
        msg.Read(ref reader);

        foreach (var kvp in msg.CVars)
        {
            _eventBus.Publish(new CVarSyncEvent(kvp.Key, kvp.Value));
        }
        return Task.CompletedTask;
    }
}
