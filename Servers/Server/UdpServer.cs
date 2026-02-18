using Shared;
using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Services;
using Shared.Interfaces;
using Core;

namespace Server
{
    public class UdpServer : EngineService, IHostedService, IUdpServer
    {
        public override int Priority => 40; // High priority
        private readonly INetworkService _networkService;
        private readonly NetworkEventHandler _networkEventHandler;
        private readonly IServerContext _context;
        private readonly BinarySnapshotService _binarySnapshotService;
        private readonly IInterestManager _interestManager;
        private readonly IJobSystem _jobSystem;
        private readonly ILogger<UdpServer> _logger;

        public UdpServer(INetworkService networkService, NetworkEventHandler networkEventHandler, IServerContext context, BinarySnapshotService binarySnapshotService, IInterestManager interestManager, IJobSystem jobSystem, ILogger<UdpServer> logger)
        {
            _networkService = networkService;
            _networkEventHandler = networkEventHandler;
            _context = context;
            _binarySnapshotService = binarySnapshotService;
            _interestManager = interestManager;
            _jobSystem = jobSystem;
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _networkEventHandler.SubscribeToEvents();
            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _networkEventHandler.UnsubscribeFromEvents();
            return Task.CompletedTask;
        }

        public void BroadcastSnapshot(string snapshot) {
            _networkService.BroadcastSnapshot(snapshot);
        }

        public void BroadcastSnapshot(MergedRegion region, string snapshot)
        {
            // We should ideally wrap this with a message type, but keeping it for compatibility
            foreach(var r in region.Regions)
                _context.PlayerManager.ForEachPlayerInRegion(r, peer => peer.SendAsync(snapshot));
        }

        public void BroadcastSnapshot(MergedRegion region, byte[] snapshot)
        {
            // Prefix with Binary message type
            byte[] message = new byte[snapshot.Length + 1];
            message[0] = (byte)SnapshotMessageType.Binary;
            Buffer.BlockCopy(snapshot, 0, message, 1, snapshot.Length);

            foreach(var r in region.Regions)
                _context.PlayerManager.ForEachPlayerInRegion(r, peer => peer.SendAsync(message));
        }

        public async Task SendRegionSnapshotAsync(MergedRegion region, System.Collections.Generic.IEnumerable<IGameObject> objects)
        {
            var players = new System.Collections.Generic.List<INetworkPeer>();
            foreach (var r in region.Regions)
            {
                _context.PlayerManager.ForEachPlayerInRegion(r, peer => players.Add(peer));
            }

            if (players.Count == 0) return;

            // Parallelize snapshot generation per player to utilize multiple cores for serialization
            await _jobSystem.ForEachAsync(players, async peer =>
            {
                // Filter objects by interest if the player has an AOI defined
                var interestedObjects = _interestManager.GetInterestedObjects(peer);
                var objectsToSend = interestedObjects.Any() ? interestedObjects : objects;

                var buffer = ArrayPool<byte>.Shared.Rent(65536);
                try
                {
                    buffer[0] = (byte)SnapshotMessageType.Binary;
                    int length = _binarySnapshotService.SerializeTo(objectsToSend, buffer, peer.LastSentVersions, 1);

                    if (length > 2) // 1 for type byte + 1 for end marker
                    {
                        await peer.SendAsync(buffer, 0, length);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            });
        }
    }
}
