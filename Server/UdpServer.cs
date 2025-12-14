using Shared;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Core;
using System.Text.Json;
using LiteNetLib;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Server
{
    public class ServerInfo
    {
        public required string ServerName { get; set; }
        public required string ServerDescription { get; set; }
        public int MaxPlayers { get; set; }
        public required string AssetUrl { get; set; }
    }

    public class UdpServer : IHostedService, IDisposable, IUdpServer
    {
        private readonly Dictionary<NetPeer, UdpNetworkPeer> _peers = new();
        private readonly NetManager _netManager;
        private readonly EventBasedNetListener _listener;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly IScriptHost _scriptHost;
        private readonly IPlayerManager _playerManager;
        private readonly ServerSettings _settings;
        private readonly ILogger<UdpServer> _logger;
        private Task? _networkTask;

        public UdpServer(IScriptHost scriptHost, IPlayerManager playerManager, ServerSettings settings, ILogger<UdpServer> logger)
        {
            _settings = settings;
            _playerManager = playerManager;
            _listener = new EventBasedNetListener();
            _netManager = new NetManager(_listener)
            {
                DisconnectTimeout = settings.Network.DisconnectTimeout
            };
            _scriptHost = scriptHost;
            _logger = logger;

            _listener.ConnectionRequestEvent += OnConnectionRequest;
            _listener.PeerConnectedEvent += OnPeerConnected;
            _listener.NetworkReceiveEvent += OnNetworkReceive;
            _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (_netManager.Start(_settings.Network.UdpPort))
            {
                _logger.LogInformation($"UDP Server started on port {_settings.Network.UdpPort}");
                _networkTask = PollEvents(_cancellationTokenSource.Token);
            }
            else
            {
                _logger.LogError("Failed to start UDP Server.");
            }
            return Task.CompletedTask;
        }

        private async Task PollEvents(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                _netManager.PollEvents();
                await Task.Delay(15, token);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_networkTask == null)
                return;

            _cancellationTokenSource?.Cancel();
            try
            {
                await _networkTask;
            }
            catch (TaskCanceledException)
            {
                // This is expected
            }
            _netManager.Stop();
            _logger.LogInformation("UDP Server stopped.");
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
            _playerManager.AddPlayer(networkPeer);

            var serverInfo = new ServerInfo
            {
                ServerName = _settings.ServerName,
                ServerDescription = _settings.ServerDescription,
                MaxPlayers = _settings.MaxPlayers,
                AssetUrl = $"http://{_settings.Network.IpAddress}:{_settings.HttpServer.Port}"
            };

            var writer = new LiteNetLib.Utils.NetDataWriter();
            writer.Put(JsonSerializer.Serialize(serverInfo));
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            if (reader.AvailableBytes > 0)
            {
                var command = reader.GetString();
                _logger.LogDebug($"Received command from {peer}: {command}");

                _scriptHost.EnqueueCommand(command, (result) => {
                    var writer = new LiteNetLib.Utils.NetDataWriter();
                    writer.Put(result);
                    peer.Send(writer, DeliveryMethod.ReliableOrdered);
                });
            }
            reader.Recycle();
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            _logger.LogInformation($"Client disconnected: {peer}. Reason: {disconnectInfo.Reason}");
            if(_peers.TryGetValue(peer, out var networkPeer))
            {
                _playerManager.RemovePlayer(networkPeer);
                _peers.Remove(peer);
            }
        }

        public void BroadcastSnapshot(string snapshot) {
            var writer = new LiteNetLib.Utils.NetDataWriter();
            writer.Put(snapshot);
            _netManager.SendToAll(writer, DeliveryMethod.Unreliable);
        }

        public void BroadcastSnapshot(Region region, string snapshot)
        {
            _playerManager.ForEachPlayerInRegion(region, peer => peer.Send(snapshot));
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
