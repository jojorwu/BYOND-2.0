using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Core;
using LiteNetLib;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Server
{
    public class UdpServer : IDisposable
    {
        private readonly NetManager _netManager;
        private readonly EventBasedNetListener _listener;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly IScriptHost _scriptHost;
        private readonly Core.GameState _gameState;
        private readonly ServerSettings _settings;
        private readonly ILogger<UdpServer> _logger;
        private Thread? _networkThread;

        public UdpServer(IPAddress ipAddress, int port, IScriptHost scriptHost, Core.GameState gameState, IOptions<ServerSettings> settings, ILogger<UdpServer> logger)
        {
            _settings = settings.Value;
            _listener = new EventBasedNetListener();
            _netManager = new NetManager(_listener)
            {
                DisconnectTimeout = _settings.Network.DisconnectTimeout
            };
            _scriptHost = scriptHost;
            _gameState = gameState;
            _cancellationTokenSource = new CancellationTokenSource();

            _listener.ConnectionRequestEvent += OnConnectionRequest;
            _listener.PeerConnectedEvent += OnPeerConnected;
            _listener.NetworkReceiveEvent += OnNetworkReceive;
            _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        }

        public void Start()
        {
            if (_netManager.Start(_settings.Network.UdpPort))
            {
                _logger.LogInformation("UDP Server started on port {UdpPort}", _settings.Network.UdpPort);
                _networkThread = new Thread(() => PollEvents(_cancellationTokenSource.Token))
                {
                    Name = "NetworkThread"
                };
                _networkThread.Start();
            }
            else
            {
                _logger.LogError("Failed to start UDP Server.");
            }
        }

        private void PollEvents(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                _netManager.PollEvents();
                Thread.Sleep(1);
            }
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _netManager.Stop();
            _logger.LogInformation("UDP Server stopped.");
        }

        private void OnConnectionRequest(ConnectionRequest request)
        {
            _logger.LogInformation("Incoming connection from {RemoteEndPoint}", request.RemoteEndPoint);
            request.AcceptIfKey(_settings.Network.ConnectionKey);
        }

        private void OnPeerConnected(NetPeer peer)
        {
            _logger.LogInformation("Client connected: {Peer}", peer);
        }

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            if (reader.AvailableBytes > 0)
            {
                var command = reader.GetString();
                _logger.LogInformation("Received command from {Peer}: {Command}", peer, command);

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
            _logger.LogInformation("Client disconnected: {Peer}. Reason: {Reason}", peer, disconnectInfo.Reason);
        }

        public void BroadcastSnapshot(string snapshot) {
            var writer = new LiteNetLib.Utils.NetDataWriter();
            writer.Put(snapshot);
            _netManager.SendToAll(writer, DeliveryMethod.Unreliable);
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource.Dispose();
        }
    }
}
