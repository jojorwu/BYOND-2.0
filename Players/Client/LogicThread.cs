using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Collections.Generic;
using Core;
using Shared;
using Shared.Interfaces;
using Shared.Utils;
using Shared.Services;
using LiteNetLib;
using Client.Networking;

namespace Client
{
    public class LogicThread
    {
        private readonly ISnapshotManager _snapshotManager;
        private readonly IStateInterpolator _stateInterpolator;
        private readonly IPacketDispatcher _packetDispatcher;
        public GameState CurrentState { get; private set; }

        private readonly object _lock = new object();
        private Thread _thread;
        private bool _isRunning;
        public const int TicksPerSecond = 30;
        public const float TimeStep = 1.0f / TicksPerSecond;
        private const double InterpolationDelay = 0.1;

        private readonly NetManager _netManager;
        private readonly EventBasedNetListener _listener;
        private readonly string _serverAddress;
        private readonly Stopwatch _gameTime = new();
        private LiteNetNetworkPeer? _serverPeer;

        public event Action<SoundData>? SoundReceived;
        public event Action<string, long?>? StopSoundReceived;
        public event Action<string, object>? CVarSyncReceived;

        public LogicThread(string serverAddress, GameState gameState, ISnapshotManager snapshotManager, IStateInterpolator stateInterpolator, IPacketDispatcher packetDispatcher, IEnumerable<IPacketHandler> handlers)
        {
            CurrentState = gameState;
            _snapshotManager = snapshotManager;
            _stateInterpolator = stateInterpolator;
            _packetDispatcher = packetDispatcher;

            foreach (var handler in handlers)
            {
                _packetDispatcher.RegisterHandler(handler);
            }

            _listener = new EventBasedNetListener();
            _netManager = new NetManager(_listener);
            _thread = new Thread(GameLoop);
            _serverAddress = serverAddress;

            _listener.NetworkReceiveEvent += OnNetworkReceive;
            _listener.PeerConnectedEvent += (peer) => _serverPeer = new LiteNetNetworkPeer(peer);
        }

        public void Start()
        {
            _isRunning = true;
            _netManager.Start();
            _gameTime.Start();

            var parts = _serverAddress.Split(':');
            var host = parts[0];
            var port = parts.Length > 1 ? int.Parse(parts[1]) : 9050;

            var writer = new LiteNetLib.Utils.NetDataWriter();
            writer.Put("BYOND2.0");
            writer.Put("Player" + Random.Shared.Next(100, 999));
            _netManager.Connect(host, port, writer);
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
            while (_isRunning)
            {
                _netManager.PollEvents();
                Thread.Sleep(5);
            }
        }

        public void SendCommand(string command)
        {
            _serverPeer?.SendAsync(command);
        }

        private async void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            if (_serverPeer == null) return;

            byte[] data = new byte[reader.AvailableBytes];
            reader.GetBytes(data, reader.AvailableBytes);

            await _packetDispatcher.DispatchAsync(_serverPeer, data);

            reader.Recycle();
        }

        public void UpdateRenderState()
        {
            double renderTime = _gameTime.Elapsed.TotalSeconds - InterpolationDelay;

            lock (_lock)
            {
                var (from, to, t) = _snapshotManager.GetInterpolationData(renderTime);
                if (from != null && to != null)
                {
                    _stateInterpolator.Interpolate(CurrentState, from, to, t);
                }
            }
        }

        public GameState GetStateForRender()
        {
            return CurrentState;
        }
    }
}
