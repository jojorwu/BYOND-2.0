using Shared;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Services;
using Shared.Interfaces;
using Core;
using Shared.Config;
using System.Linq;

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
        private readonly IConfigurationManager _configManager;

        public UdpServer(INetworkService networkService, NetworkEventHandler networkEventHandler, IServerContext context, BinarySnapshotService binarySnapshotService, IInterestManager interestManager, IJobSystem jobSystem, IConfigurationManager configManager)
        {
            _networkService = networkService;
            _networkEventHandler = networkEventHandler;
            _context = context;
            _binarySnapshotService = binarySnapshotService;
            _interestManager = interestManager;
            _jobSystem = jobSystem;
            _configManager = configManager;
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

        public void BroadcastSnapshot(byte[] snapshot) {
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

        public async Task SendRegionSnapshotAsync(MergedRegion region, System.Collections.Generic.IEnumerable<IGameObject> objects)
        {
            var players = new System.Collections.Generic.List<INetworkPeer>();
            foreach (var r in region.Regions)
            {
                _context.PlayerManager.ForEachPlayerInRegion(r, peer => players.Add(peer));
            }

            if (players.Count == 0) return;

            // Parallelize snapshot generation per player to utilize multiple cores for serialization
            await _jobSystem.ForEachAsync(players, peer =>
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

        public void SendSound(INetworkPeer peer, SoundData sound)
        {
            byte[] data = SerializeSound(sound);
            _ = peer.SendAsync(data);
        }

        public void BroadcastSound(SoundData sound)
        {
            byte[] data = SerializeSound(sound);
            _networkService.BroadcastSnapshot(data); // Using BroadcastSnapshot for general byte broadcast
        }

        public void BroadcastSound(SoundData sound, Region region)
        {
            byte[] data = SerializeSound(sound);
            _context.PlayerManager.ForEachPlayerInRegion(region, peer => _ = peer.SendAsync(data));
        }

        public void BroadcastSound(SoundData sound, MergedRegion mergedRegion)
        {
            byte[] data = SerializeSound(sound);
            foreach (var r in mergedRegion.Regions)
            {
                _context.PlayerManager.ForEachPlayerInRegion(r, peer => _ = peer.SendAsync(data));
            }
        }

        private byte[] SerializeSound(SoundData sound)
        {
            using var ms = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(ms);
            writer.Write((byte)SnapshotMessageType.Sound);
            writer.Write(sound.File);
            writer.Write(sound.Volume);
            writer.Write(sound.Pitch);
            writer.Write(sound.Repeat);
            writer.Write(sound.X.HasValue);
            if (sound.X.HasValue) writer.Write(sound.X.Value);
            writer.Write(sound.Y.HasValue);
            if (sound.Y.HasValue) writer.Write(sound.Y.Value);
            writer.Write(sound.Z.HasValue);
            if (sound.Z.HasValue) writer.Write(sound.Z.Value);
            writer.Write(sound.ObjectId.HasValue);
            if (sound.ObjectId.HasValue) writer.Write(sound.ObjectId.Value);
            writer.Write(sound.Falloff);
            return ms.ToArray();
        }

        public void StopSound(string file, Region? region = null)
        {
            byte[] data = SerializeStopSound(file, null);
            if (region != null)
                _context.PlayerManager.ForEachPlayerInRegion(region, peer => _ = peer.SendAsync(data));
            else
                _networkService.BroadcastSnapshot(data);
        }

        public void StopSoundOn(string file, long objectId, Region? region = null)
        {
            byte[] data = SerializeStopSound(file, objectId);
            if (region != null)
                _context.PlayerManager.ForEachPlayerInRegion(region, peer => _ = peer.SendAsync(data));
            else
                _networkService.BroadcastSnapshot(data);
        }

        private byte[] SerializeStopSound(string file, long? objectId)
        {
            using var ms = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(ms);
            writer.Write((byte)SnapshotMessageType.StopSound);
            writer.Write(file);
            writer.Write(objectId.HasValue);
            if (objectId.HasValue) writer.Write(objectId.Value);
            return ms.ToArray();
        }

        public void SendCVars(INetworkPeer peer)
        {
            var replicatedCVars = _configManager.GetRegisteredCVars()
                .Where(c => (c.Flags & CVarFlags.Replicated) != 0)
                .ToDictionary(c => c.Name, c => c.Value);

            if (replicatedCVars.Count == 0) return;

            using var ms = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(ms);
            writer.Write((byte)SnapshotMessageType.SyncCVars);

            string json = System.Text.Json.JsonSerializer.Serialize(replicatedCVars);
            writer.Write(json);

            _ = peer.SendAsync(ms.ToArray());
        }
    }
}
