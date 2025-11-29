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
        private Timer _snapshotTimer;

        public UdpServer(IPAddress ipAddress, int port, ScriptHost scriptHost, Core.GameState gameState)
        {
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
            if (_netManager.Start(9050)) // TODO: Use port from config
            {
                Console.WriteLine("UDP Server started on port 9050");
                Task.Run(() => PollEvents(_cancellationTokenSource.Token));
                _snapshotTimer.Change(0, 100); // Start broadcasting snapshots every 100ms
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
                Thread.Sleep(15);
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
            request.AcceptIfKey("BYOND2.0");
        }

        private void OnPeerConnected(NetPeer peer)
        {
            Console.WriteLine($"Client connected: {peer}");
        }

        private void BroadcastSnapshot(object? state)
        {
            var writer = new LiteNetLib.Utils.NetDataWriter();
            writer.Put((byte)Core.SnapshotMessageType.Full);
            writer.Put(_gameState.GameObjects.Count);
            foreach (var gameObject in _gameState.GameObjects.Values)
            {
                writer.Put(gameObject.Id);
                writer.Put(gameObject.Position.X);
                writer.Put(gameObject.Position.Y);

                // Send icon path, or empty string if not specified
                var icon = gameObject.ObjectType.GetProperty<string>("icon");
                writer.Put(icon ?? string.Empty);
            }
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
        }
    }
}
