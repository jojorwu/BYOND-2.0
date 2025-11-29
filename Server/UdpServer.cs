using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib;

namespace Server
{
    public class UdpServer : IDisposable
    {
        private readonly NetManager _netManager;
        private readonly EventBasedNetListener _listener;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ScriptHost _scriptHost;
        private readonly Core.GameState _gameState;
        private readonly Core.ServerSettings _settings;
        private readonly SnapshotManager _snapshotManager;
        private Timer _snapshotTimer;

        public UdpServer(IPAddress ipAddress, int port, ScriptHost scriptHost, Core.GameState gameState, Core.ServerSettings settings)
        {
            _settings = settings;
            _snapshotManager = new SnapshotManager(gameState);
            _listener = new EventBasedNetListener();
            _netManager = new NetManager(_listener)
            {
                DisconnectTimeout = 10000
            };
            _scriptHost = scriptHost;
            _gameState = gameState;
            _cancellationTokenSource = new CancellationTokenSource();
            _snapshotTimer = new Timer(BroadcastSnapshot, null, Timeout.Infinite, Timeout.Infinite);

            _listener.ConnectionRequestEvent += OnConnectionRequest;
            _listener.PeerConnectedEvent += OnPeerConnected;
            _listener.NetworkReceiveEvent += OnNetworkReceive;
            _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        }

        public void Start()
        {
            if (_netManager.Start(_settings.Network.UdpPort))
            {
                Console.WriteLine($"UDP Server started on port {_settings.Network.UdpPort}");
                Task.Run(() => PollEvents(_cancellationTokenSource.Token));
                _snapshotTimer.Change(0, _settings.Performance.SnapshotBroadcastInterval);
            }
            else
            {
                Console.WriteLine("Failed to start UDP Server.");
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
            _snapshotTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _cancellationTokenSource.Cancel();
            _netManager.Stop();
            Console.WriteLine("UDP Server stopped.");
        }

        private void OnConnectionRequest(ConnectionRequest request)
        {
            Console.WriteLine($"Incoming connection from {request.RemoteEndPoint}");
            request.AcceptIfKey(_settings.Network.ConnectionKey);
        }

        private void OnPeerConnected(NetPeer peer)
        {
            Console.WriteLine($"Client connected: {peer}");
        }

        private void BroadcastSnapshot(object? state)
        {
            var writer = _snapshotManager.CreateSnapshot();
            _netManager.SendToAll(writer, DeliveryMethod.ReliableOrdered);
        }

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            // For now, we only process commands from clients, not other message types.
            // The snapshot logic is one-way (server to client).
            if(reader.AvailableBytes > 0)
            {
                var command = reader.GetString();
                Console.WriteLine($"Received command from {peer}: {command}");

                string result;
                if (command == "ping")
                {
                    result = "pong";
                }
                else
                {
                    result = _scriptHost.ExecuteCommand(command);
                }

                var writer = new LiteNetLib.Utils.NetDataWriter();
                writer.Put(result);
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
            reader.Recycle();
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            Console.WriteLine($"Client disconnected: {peer}. Reason: {disconnectInfo.Reason}");
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource.Dispose();
            _snapshotTimer.Dispose();
        }
    }
}
