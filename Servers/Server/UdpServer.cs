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
using Shared.Utils;
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

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            _networkEventHandler.SubscribeToEvents();
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
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
            foreach(var r in region.Regions)
                _context.PlayerManager.ForEachPlayerInRegion(r, peer => _ = peer.SendAsync(snapshot));
        }

        public void BroadcastSnapshot(MergedRegion region, byte[] snapshot)
        {
            byte[] message = new byte[snapshot.Length + 1];
            message[0] = (byte)SnapshotMessageType.BitPackedDelta;
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

            await _jobSystem.ForEachAsync(players, peer =>
            {
                var interestedObjects = _interestManager.GetInterestedObjects(peer);
                var objectsToSend = interestedObjects.IsDefault ? objects : (IEnumerable<IGameObject>)interestedObjects;
                int bufferSize = 65536;
                while (true)
                {
                    byte[] rented = ArrayPool<byte>.Shared.Rent(bufferSize);
                    try
                    {
                        int written = _binarySnapshotService.SerializeBitPackedDelta(rented.AsSpan(1), objectsToSend, peer.LastSentVersions, out bool truncated);
                        if (!truncated)
                        {
                            if (written > 0)
                            {
                                rented[0] = (byte)SnapshotMessageType.BitPackedDelta;
                                _ = peer.SendAsync(new ReadOnlyMemory<byte>(rented, 0, written + 1));
                            }
                            break;
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                    }
                    bufferSize *= 2;
                    if (bufferSize > 1024 * 1024 * 10) break;
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
            _networkService.BroadcastSnapshot(data);
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
            byte[] buffer = new byte[1024];
            var writer = new BitWriter(buffer);
            writer.WriteBits((byte)SnapshotMessageType.Sound, 8);
            writer.WriteString(sound.File);
            writer.WriteDouble(sound.Volume);
            writer.WriteDouble(sound.Pitch);
            writer.WriteBool(sound.Repeat);
            writer.WriteBool(sound.X.HasValue);
            if (sound.X.HasValue) writer.WriteZigZag(sound.X.Value);
            writer.WriteBool(sound.Y.HasValue);
            if (sound.Y.HasValue) writer.WriteZigZag(sound.Y.Value);
            writer.WriteBool(sound.Z.HasValue);
            if (sound.Z.HasValue) writer.WriteZigZag(sound.Z.Value);
            writer.WriteBool(sound.ObjectId.HasValue);
            if (sound.ObjectId.HasValue) writer.WriteVarInt(sound.ObjectId.Value);
            writer.WriteDouble(sound.Falloff);

            byte[] result = new byte[writer.BytesWritten];
            buffer.AsSpan(0, writer.BytesWritten).CopyTo(result);
            return result;
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
            byte[] buffer = new byte[512];
            var writer = new BitWriter(buffer);
            writer.WriteBits((byte)SnapshotMessageType.StopSound, 8);
            writer.WriteString(file);
            writer.WriteBool(objectId.HasValue);
            if (objectId.HasValue) writer.WriteVarInt(objectId.Value);

            byte[] result = new byte[writer.BytesWritten];
            buffer.AsSpan(0, writer.BytesWritten).CopyTo(result);
            return result;
        }

        public void SendCVars(INetworkPeer peer)
        {
            var replicatedCVars = _configManager.GetRegisteredCVars()
                .Where(c => (c.Flags & CVarFlags.Replicated) != 0)
                .ToDictionary(c => c.Name, c => c.Value);

            if (replicatedCVars.Count == 0) return;

            byte[] buffer = new byte[4096];
            var writer = new BitWriter(buffer);
            writer.WriteBits((byte)SnapshotMessageType.SyncCVars, 8);
            string json = System.Text.Json.JsonSerializer.Serialize(replicatedCVars);
            writer.WriteString(json);

            byte[] result = new byte[writer.BytesWritten];
            buffer.AsSpan(0, writer.BytesWritten).CopyTo(result);
            _ = peer.SendAsync(result);
        }
    }
}
