using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib;
using Microsoft.Extensions.Logging;
using Shared;
using Server.Events;
using Shared.Messaging;
using Shared.Services;

namespace Server
{
    public class NetworkService : EngineService, INetworkService, IDisposable
    {
        public override int Priority => 45;
        private readonly NetManager _netManager;
        private readonly EventBasedNetListener _listener;
        private readonly IServerContext _context;
        private readonly ILogger<NetworkService> _logger;
        private readonly IEventBus _eventBus;
        private readonly Dictionary<NetPeer, UdpNetworkPeer> _peers = new();
        private readonly NetDataWriterPool _writerPool;
        private Task? _networkTask;
        private CancellationTokenSource? _cancellationTokenSource;

        public event Action<INetworkPeer>? PeerConnected;
        public event Action<INetworkPeer, DisconnectInfo>? PeerDisconnected;
        public event Action<INetworkPeer, string>? CommandReceived;

        public NetworkService(IServerContext context, ILogger<NetworkService> logger, NetDataWriterPool writerPool, IEventBus eventBus)
        {
            _context = context;
            _logger = logger;
            _writerPool = writerPool;
            _eventBus = eventBus;
            _listener = new EventBasedNetListener();
            _netManager = new NetManager(_listener)
            {
                DisconnectTimeout = _context.Settings.Network.DisconnectTimeout
            };

            _listener.ConnectionRequestEvent += OnConnectionRequest;
            _listener.PeerConnectedEvent += OnPeerConnected;
            _listener.NetworkReceiveEvent += OnNetworkReceive;
            _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (_netManager.Start(_context.Settings.Network.UdpPort))
            {
                _logger.LogInformation($"Network service started on port {_context.Settings.Network.UdpPort}");
                _networkTask = Task.Run(() => PollEvents(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            }
            else
            {
                _logger.LogError("Failed to start network service.");
            }
            return Task.CompletedTask;
        }

        public void Start() => StartAsync(CancellationToken.None);

        private async Task PollEvents(CancellationToken token)
        {
            _logger.LogInformation("Network event polling started.");
            int pruneCounter = 0;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    _netManager.PollEvents();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error polling network events.");
                }

                // Prune every ~30 seconds (5ms * 6000 = 30000ms)
                if (++pruneCounter >= 6000)
                {
                    pruneCounter = 0;
                    PruneAllPeers();
                }

                // Lower delay for better responsiveness, but enough to avoid CPU pinning
                await Task.Delay(5, token);
            }
        }

        private readonly List<int> _pruneIdBuffer = new(4096);
        private void PruneAllPeers()
        {
            _pruneIdBuffer.Clear();
            _context.GameState.ForEachGameObject(o => _pruneIdBuffer.Add(o.Id));

            foreach (var peer in _peers.Values)
            {
                peer.PruneLastSentVersions(_pruneIdBuffer);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource?.Cancel();
            if (_networkTask != null)
            {
                try
                {
                    await _networkTask;
                }
                catch (OperationCanceledException) { }
            }
            _netManager.Stop();

            _listener.ConnectionRequestEvent -= OnConnectionRequest;
            _listener.PeerConnectedEvent -= OnPeerConnected;
            _listener.NetworkReceiveEvent -= OnNetworkReceive;
            _listener.PeerDisconnectedEvent -= OnPeerDisconnected;

            _logger.LogInformation("Network service stopped.");
        }

        public void Stop() => StopAsync(CancellationToken.None).Wait();

        private void OnConnectionRequest(ConnectionRequest request)
        {
            _logger.LogInformation($"Incoming connection from {request.RemoteEndPoint}");
            request.AcceptIfKey(_context.Settings.Network.ConnectionKey);
        }

        private void OnPeerConnected(NetPeer peer)
        {
            _logger.LogInformation($"Client connected: {peer}");
            var networkPeer = new UdpNetworkPeer(peer, _writerPool);
            _peers[peer] = networkPeer;

            PeerConnected?.Invoke(networkPeer);
            _eventBus.Publish(new PeerConnectedEvent(networkPeer));
        }

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            _context.PerformanceMonitor.RecordBytesReceived(reader.AvailableBytes);
            if (reader.AvailableBytes > 0)
            {
                // Safety limit for command length
                const int MaxCommandLength = 4096;
                var command = reader.GetString(MaxCommandLength);

                if(_peers.TryGetValue(peer, out var networkPeer))
                {
                    CommandReceived?.Invoke(networkPeer, command);
                    _eventBus.Publish(new CommandReceivedEvent(networkPeer, command));
                }
            }
            reader.Recycle();
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            _logger.LogInformation($"Client disconnected: {peer}. Reason: {disconnectInfo.Reason}");
            if(_peers.TryGetValue(peer, out var networkPeer))
            {
                PeerDisconnected?.Invoke(networkPeer, disconnectInfo);
                _eventBus.Publish(new PeerDisconnectedEvent(networkPeer, disconnectInfo));
                _peers.Remove(peer);
            }
        }

        public void BroadcastSnapshot(string snapshot) {
            var writer = _writerPool.Rent();
            try
            {
                writer.Put(snapshot);
                _context.PerformanceMonitor.RecordBytesSent(writer.Length);
                _netManager.SendToAll(writer, DeliveryMethod.Unreliable);
            }
            finally
            {
                _writerPool.Return(writer);
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
