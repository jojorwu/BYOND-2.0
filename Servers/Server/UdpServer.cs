using Shared;
using System;
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

        public UdpServer(INetworkService networkService, NetworkEventHandler networkEventHandler, IServerContext context, BinarySnapshotService binarySnapshotService, IInterestManager interestManager)
        {
            _networkService = networkService;
            _networkEventHandler = networkEventHandler;
            _context = context;
            _binarySnapshotService = binarySnapshotService;
            _interestManager = interestManager;
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
                _context.PlayerManager.ForEachPlayerInRegion(r, peer => _ = peer.SendAsync(snapshot));
        }

        public void BroadcastSnapshot(MergedRegion region, byte[] snapshot)
        {
            // Prefix with Binary message type
            byte[] message = new byte[snapshot.Length + 1];
            message[0] = (byte)SnapshotMessageType.Binary;
            Buffer.BlockCopy(snapshot, 0, message, 1, snapshot.Length);

            foreach(var r in region.Regions)
                _context.PlayerManager.ForEachPlayerInRegion(r, peer => _ = peer.SendAsync(message));
        }

        public void SendRegionSnapshot(MergedRegion region, IEnumerable<IGameObject> objects)
        {
            foreach (var r in region.Regions)
            {
                _context.PlayerManager.ForEachPlayerInRegion(r, peer =>
                {
                    // Filter objects by interest if the player has an AOI defined
                    var interestedObjects = _interestManager.GetInterestedObjects(peer);
                    var objectsToSend = interestedObjects.Any() ? interestedObjects : objects;

                    byte[] snapshot = _binarySnapshotService.Serialize(objectsToSend, peer.LastSentVersions);
                    if (snapshot.Length > 1) // 1 byte for end marker
                    {
                        byte[] message = new byte[snapshot.Length + 1];
                        message[0] = (byte)SnapshotMessageType.Binary;
                        Buffer.BlockCopy(snapshot, 0, message, 1, snapshot.Length);
                        _ = peer.SendAsync(message);
                    }
                });
            }
        }
    }
}
