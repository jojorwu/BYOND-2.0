using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using Shared;
using Shared.Interfaces;
using Shared.Services;
using Shared.Events;
using Shared.Messaging;
using Shared.Utils;
using Core;

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

        var json = reader.ReadString();
        var cvars = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        if (cvars != null)
        {
            foreach (var kvp in cvars)
            {
                _eventBus.Publish(new CVarSyncEvent(kvp.Key, kvp.Value));
            }
        }
        return Task.CompletedTask;
    }
}
