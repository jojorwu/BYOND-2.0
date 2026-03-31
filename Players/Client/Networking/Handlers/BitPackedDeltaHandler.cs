using System.Threading.Tasks;
using System;
using Shared;
using Shared.Interfaces;
using Shared.Services;
using Shared.Models;
using Core;
using Client.Services;

namespace Client.Networking.Handlers;

public class BitPackedDeltaHandler : BasePacketHandler
{
    private readonly ISnapshotSerializer _serializer;
    private readonly ISnapshotManager _snapshotManager;
    private readonly IClientObjectManager _objectManager;

    public override byte PacketTypeId => (byte)SnapshotMessageType.BitPackedDelta;

    public BitPackedDeltaHandler(ISnapshotSerializer serializer, ISnapshotManager snapshotManager, IClientObjectManager objectManager)
    {
        _serializer = serializer;
        _snapshotManager = snapshotManager;
        _objectManager = objectManager;
    }

    public override Task HandleAsync(INetworkPeer peer, ReadOnlyMemory<byte> data)
    {
        _serializer.DeserializeBitPacked(data.Span, _objectManager.World, null!, null!);
        _snapshotManager.AddSnapshot(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0, _objectManager.World.Values);
        return Task.CompletedTask;
    }
}
