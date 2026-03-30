using System;
using System.Threading.Tasks;
using Shared;
using Shared.Interfaces;
using Shared.Services;
using Core;

namespace Client.Networking.Handlers;

public class BitPackedDeltaHandler : BasePacketHandler
{
    private readonly GameState _gameState;
    private readonly ISnapshotSerializer _serializer;
    private readonly IObjectTypeManager _typeManager;
    private readonly IObjectFactory _objectFactory;
    private readonly ISnapshotManager _snapshotManager;

    public override byte PacketTypeId => (byte)SnapshotMessageType.BitPackedDelta;

    public BitPackedDeltaHandler(GameState gameState, ISnapshotSerializer serializer, IObjectTypeManager typeManager, IObjectFactory objectFactory, ISnapshotManager snapshotManager)
    {
        _gameState = gameState;
        _serializer = serializer;
        _typeManager = typeManager;
        _objectFactory = objectFactory;
        _snapshotManager = snapshotManager;
    }

    public override Task HandleAsync(INetworkPeer peer, ReadOnlyMemory<byte> data)
    {
        _serializer.DeserializeBitPacked(data.Span, _gameState.GameObjects, _typeManager, _objectFactory);
        _snapshotManager.AddSnapshot(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0, _gameState.GameObjects.Values);
        return Task.CompletedTask;
    }
}
