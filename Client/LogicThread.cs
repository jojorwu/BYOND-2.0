using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using Core;
using LiteNetLib;

namespace Client
{
    public class LogicThread
    {
        public GameState PreviousState { get; private set; }
        public GameState CurrentState { get; private set; }

        private readonly object _lock = new object();
        private Thread _thread;
        private bool _isRunning;
        public const int TicksPerSecond = 30;
        public const float TimeStep = 1.0f / TicksPerSecond;

        private readonly NetManager _netManager;
        private readonly EventBasedNetListener _listener;
        private readonly string _serverAddress;
        private NetPeer? _server;

        public LogicThread(string serverAddress)
        {
            PreviousState = new GameState();
            CurrentState = new GameState();
            _listener = new EventBasedNetListener();
            _netManager = new NetManager(_listener);
            _thread = new Thread(GameLoop);
            _serverAddress = serverAddress;

            _listener.NetworkReceiveEvent += OnNetworkReceive;
        }

        public void Start()
        {
            _isRunning = true;
            _netManager.Start();

            var parts = _serverAddress.Split(':');
            var host = parts[0];
            var port = parts.Length > 1 ? int.Parse(parts[1]) : 9050; // Default port

            _netManager.Connect(host, port, "BYOND2.0");
            _thread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
            _netManager.Stop();
            _thread.Join();
        }

        private void GameLoop()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            double lastTime = stopwatch.Elapsed.TotalSeconds;
            double accumulator = 0;

            while (_isRunning)
            {
                _netManager.PollEvents();

                double currentTime = stopwatch.Elapsed.TotalSeconds;
                double frameTime = currentTime - lastTime;
                lastTime = currentTime;
                accumulator += frameTime;

                while (accumulator >= TimeStep)
                {
                    // Update logic is now driven by server responses
                    accumulator -= TimeStep;
                }

                Thread.Sleep(15);
            }
        }

        public void SendCommand(string command)
        {
            if (_netManager.FirstPeer != null && _netManager.FirstPeer.ConnectionState == ConnectionState.Connected)
            {
                var writer = new LiteNetLib.Utils.NetDataWriter();
                writer.Put(command);
                _netManager.FirstPeer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            // First byte is the message type. We may have other types later.
            var messageType = (SnapshotMessageType)reader.GetByte();

            if (messageType == SnapshotMessageType.Full)
            {
                var json = reader.GetString();
                var newGameState = JsonSerializer.Deserialize<GameState>(json);

                if (newGameState != null) {
                    lock (_lock)
                    {
                        PreviousState = CurrentState;
                        CurrentState = newGameState;
                    }
                }
            } else {
                // We might receive other message types like commands responses, etc.
                // For now, we just print them if they are not snapshots.
                // Note: a robust implementation would have a proper message dispatcher.
                try {
                    var text = reader.GetString();
                    Console.WriteLine($"Received non-snapshot message: {text}");
                } catch {
                     Console.WriteLine("Received unknown binary message.");
                }
            }

            reader.Recycle();
        }

        public (GameState, GameState) GetStatesForRender()
        {
            lock (_lock)
            {
                return (PreviousState, CurrentState);
            }
        }
    }
}
