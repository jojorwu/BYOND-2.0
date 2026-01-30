using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib;
using Microsoft.Extensions.Logging;
using Shared;

namespace Server
{
    public class NetworkService : INetworkService, IDisposable
    {
        private readonly NetManager _netManager;
        private readonly EventBasedNetListener _listener;
        private readonly IServerContext _context;
        private readonly ILogger<NetworkService> _logger;
        private readonly Dictionary<NetPeer, UdpNetworkPeer> _peers = new();
        private readonly NetDataWriterPool _writerPool = new();
        private Task? _networkTask;
        private CancellationTokenSource? _cancellationTokenSource;

        public event Action<INetworkPeer>? PeerConnected;
        public event Action<INetworkPeer, DisconnectInfo>? PeerDisconnected;
        public event Action<INetworkPeer, string>? CommandReceived;

        public NetworkService(IServerContext context, ILogger<NetworkService> logger)
        {
            _context = context;
            _logger = logger;
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

        public void Start()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            if (_netManager.Start(_context.Settings.Network.UdpPort))
            {
                _logger.LogInformation($"Network service started on port {_context.Settings.Network.UdpPort}");
                _networkTask = PollEvents(_cancellationTokenSource.Token);
            }
            else
            {
                _logger.LogError("Failed to start network service.");
            }
        }

        private async Task PollEvents(CancellationToken token)
        {
            _logger.LogInformation("Network event polling started.");
            while (!token.IsCancellationRequested)
            {
                _netManager.PollEvents();
                // Lower delay for better responsiveness, but enough to avoid CPU pinning
                await Task.Delay(5, token);
            }
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
            _netManager.Stop();
            _logger.LogInformation("Network service stopped.");
        }

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
        }

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            _context.PerformanceMonitor.RecordBytesReceived(reader.AvailableBytes);
            if (reader.AvailableBytes > 0)
            {
                var command = reader.GetString();
                if(_peers.TryGetValue(peer, out var networkPeer))
                    CommandReceived?.Invoke(networkPeer, command);
            }
            reader.Recycle();
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            _logger.LogInformation($"Client disconnected: {peer}. Reason: {disconnectInfo.Reason}");
            if(_peers.TryGetValue(peer, out var networkPeer))
            {
                PeerDisconnected?.Invoke(networkPeer, disconnectInfo);
                _peers.Remove(peer);
            }
        }

        public void BroadcastSnapshot(string snapshot) {
            var writer = _writerPool.Get();
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
