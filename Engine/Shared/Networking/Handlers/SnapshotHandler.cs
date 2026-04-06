using Shared.Interfaces;
using Shared.Utils;
using Shared.Networking;
using Shared.Networking.Messages;
using Shared.Models;
using Microsoft.Extensions.DependencyInjection;

using Shared.Buffers;
namespace Shared.Networking.Handlers;

public class SnapshotHandler : IPacketHandler
{
    private readonly ISnapshotSerializer _serializer;
    private readonly ISnapshotManager _snapshotManager;
    private readonly IObjectTypeManager _typeManager;
    private readonly IObjectFactory _objectFactory;
    private readonly IServiceProvider _serviceProvider;

    public byte PacketTypeId => (byte)NetworkMessageType.Snapshot;

    public SnapshotHandler(ISnapshotSerializer serializer, ISnapshotManager snapshotManager, IObjectTypeManager typeManager, IObjectFactory objectFactory, IServiceProvider serviceProvider)
    {
        _serializer = serializer;
        _snapshotManager = snapshotManager;
        _typeManager = typeManager;
        _objectFactory = objectFactory;
        _serviceProvider = serviceProvider;
    }

    public Task HandleAsync(INetworkPeer peer, ReadOnlyMemory<byte> data)
    {
        var world = _serviceProvider.GetService<IGameState>()?.GameObjects ?? new Dictionary<long, GameObject>();
        var reader = new BitReader(data.Span);
        _serializer.DeserializeBitPacked(ref reader, world, _typeManager, _objectFactory);
        _snapshotManager.AddSnapshot(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0, world.Values);
        return Task.CompletedTask;
    }
}
