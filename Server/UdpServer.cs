using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Core;
using LiteNetLib;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Server
{
    public class UdpServer : IHostedService, IDisposable
    {
        private readonly NetManager _netManager;
        private readonly EventBasedNetListener _listener;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly IScriptHost _scriptHost;
        private readonly GameState _gameState;
        private readonly ServerSettings _settings;
        private readonly ILogger<UdpServer> _logger;
        private Task? _networkTask;

        public UdpServer(IScriptHost scriptHost, GameState gameState, ServerSettings settings, ILogger<UdpServer> logger)
        {
            _settings = settings;
            _listener = new EventBasedNetListener();
            _netManager = new NetManager(_listener)
            {
                DisconnectTimeout = settings.Network.DisconnectTimeout
            };
            _scriptHost = scriptHost;
            _gameState = gameState;
            _logger = logger;

            _listener.ConnectionRequestEvent += OnConnectionRequest;
            _listener.PeerConnectedEvent += OnPeerConnected;
            _listener.NetworkReceiveEvent += OnNetworkReceive;
            _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_netManager.Start(_settings.Network.UdpPort))
            {
                _logger.LogInformation($"UDP Server started on port {_settings.Network.UdpPort}");
                _networkTask = Task.Run(() => PollEvents(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            }
            else
            {
                _logger.LogError("Failed to start UDP Server.");
            }
            return Task.CompletedTask;
        }

        private void PollEvents(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                _netManager.PollEvents();
                Thread.Sleep(15);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource.Cancel();
            _netManager.Stop();
            _logger.LogInformation("UDP Server stopped.");
            return Task.CompletedTask;
        }

        private void OnConnectionRequest(ConnectionRequest request)
        {
            _logger.LogInformation($"Incoming connection from {request.RemoteEndPoint}");
            request.AcceptIfKey(_settings.Network.ConnectionKey);
        }

        private void OnPeerConnected(NetPeer peer)
        {
            _logger.LogInformation($"Client connected: {peer}");
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
        }

        public virtual void BroadcastSnapshot(string snapshot) {
            var writer = new LiteNetLib.Utils.NetDataWriter();
            writer.Put(snapshot);
            _netManager.SendToAll(writer, DeliveryMethod.Unreliable);
        }

        public void Dispose()
        {
            _cancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
