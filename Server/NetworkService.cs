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
        private readonly ServerSettings _settings;
        private readonly ILogger<NetworkService> _logger;
        private readonly Dictionary<NetPeer, UdpNetworkPeer> _peers = new();
        private Task? _networkTask;
        private CancellationTokenSource? _cancellationTokenSource;

        public event Action<INetworkPeer>? PeerConnected;
        public event Action<INetworkPeer, DisconnectInfo>? PeerDisconnected;
        public event Action<INetworkPeer, string>? CommandReceived;

        public NetworkService(ServerSettings settings, ILogger<NetworkService> logger)
        {
            _settings = settings;
            _logger = logger;
            _listener = new EventBasedNetListener();
            _netManager = new NetManager(_listener)
            {
                DisconnectTimeout = settings.Network.DisconnectTimeout
            };

            _listener.ConnectionRequestEvent += OnConnectionRequest;
            _listener.PeerConnectedEvent += OnPeerConnected;
            _listener.NetworkReceiveEvent += OnNetworkReceive;
            _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        }

        public void Start()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            if (_netManager.Start(_settings.Network.UdpPort))
            {
                _logger.LogInformation($"Network service started on port {_settings.Network.UdpPort}");
                _networkTask = PollEvents(_cancellationTokenSource.Token);
            }
            else
            {
                _logger.LogError("Failed to start network service.");
            }
        }

        private async Task PollEvents(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                _netManager.PollEvents();
                await Task.Delay(15, token);
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
            request.AcceptIfKey(_settings.Network.ConnectionKey);
        }

        private void OnPeerConnected(NetPeer peer)
        {
            _logger.LogInformation($"Client connected: {peer}");
            var networkPeer = new UdpNetworkPeer(peer);
            _peers[peer] = networkPeer;
            PeerConnected?.Invoke(networkPeer);
        }

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
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
            var writer = new LiteNetLib.Utils.NetDataWriter();
            writer.Put(snapshot);
            _netManager.SendToAll(writer, DeliveryMethod.Unreliable);
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
