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
using System.Buffers;

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
        private readonly NetDataWriterPool _writerPool;

        public UdpServer(INetworkService networkService, NetworkEventHandler networkEventHandler, IServerContext context, BinarySnapshotService binarySnapshotService, IInterestManager interestManager, IJobSystem jobSystem, IConfigurationManager configManager, NetDataWriterPool writerPool)
        {
            _networkService = networkService;
            _networkEventHandler = networkEventHandler;
            _context = context;
            _binarySnapshotService = binarySnapshotService;
            _interestManager = interestManager;
            _jobSystem = jobSystem;
            _configManager = configManager;
            _writerPool = writerPool;
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

                // Use the modern SerializeTo API with a pooled buffer
                var objectsToSend = interestedObjects.IsDefault ? objects : (IEnumerable<IGameObject>)interestedObjects;
                int bufferSize = 65536;
                while (true)
                {
                    byte[] rented = ArrayPool<byte>.Shared.Rent(bufferSize);
                    try
                    {
                        int written = _binarySnapshotService.SerializeTo(rented, objectsToSend, peer.LastSentVersions, out bool truncated);
                        if (!truncated)
                        {
                            if (written > 0)
                            {
                                byte[] message = new byte[written + 1];
                                message[0] = (byte)SnapshotMessageType.Binary;
                                Buffer.BlockCopy(rented, 0, message, 1, written);
                                _ = peer.SendAsync(message);
                            }
                            break;
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                    }
                    bufferSize *= 2;
                    if (bufferSize > 1024 * 1024 * 10) break; // 10MB limit per player snapshot
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
            var writer = _writerPool.Rent();
            try
            {
                writer.Put((byte)SnapshotMessageType.Sound);
                writer.Put(sound.File);
                writer.Put(sound.Volume);
                writer.Put(sound.Pitch);
                writer.Put(sound.Repeat);
                writer.Put(sound.X.HasValue);
                if (sound.X.HasValue) writer.Put(sound.X.Value);
                writer.Put(sound.Y.HasValue);
                if (sound.Y.HasValue) writer.Put(sound.Y.Value);
                writer.Put(sound.Z.HasValue);
                if (sound.Z.HasValue) writer.Put(sound.Z.Value);
                writer.Put(sound.ObjectId.HasValue);
                if (sound.ObjectId.HasValue) writer.Put(sound.ObjectId.Value);
                writer.Put(sound.Falloff);
                return writer.CopyData();
            }
            finally
            {
                _writerPool.Return(writer);
            }
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
            var writer = _writerPool.Rent();
            try
            {
                writer.Put((byte)SnapshotMessageType.StopSound);
                writer.Put(file);
                writer.Put(objectId.HasValue);
                if (objectId.HasValue) writer.Put(objectId.Value);
                return writer.CopyData();
            }
            finally
            {
                _writerPool.Return(writer);
            }
        }

        public void SendCVars(INetworkPeer peer)
        {
            var replicatedCVars = _configManager.GetRegisteredCVars()
                .Where(c => (c.Flags & CVarFlags.Replicated) != 0)
                .ToDictionary(c => c.Name, c => c.Value);

            if (replicatedCVars.Count == 0) return;

            var writer = _writerPool.Rent();
            try
            {
                writer.Put((byte)SnapshotMessageType.SyncCVars);
                string json = System.Text.Json.JsonSerializer.Serialize(replicatedCVars);
                writer.Put(json);
                _ = peer.SendAsync(writer.CopyData());
            }
            finally
            {
                _writerPool.Return(writer);
            }
        }
    }
}
