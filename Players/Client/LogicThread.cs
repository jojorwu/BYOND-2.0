using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Collections.Generic;
using Core;
using Shared;
using Shared.Interfaces;
using Shared.Utils;
using LiteNetLib;
using Client.Services;

namespace Client
{
    public class LogicThread
    {
        private readonly ISnapshotManager _snapshotManager;
        private readonly IStateInterpolator _stateInterpolator;
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
        private readonly IObjectTypeManager _typeManager;
        private readonly IObjectFactory _objectFactory;
        private readonly Shared.Services.BinarySnapshotService _binaryService;
        private readonly Stopwatch _gameTime = new();

        public event Action<SoundData>? SoundReceived;
        public event Action<string, long?>? StopSoundReceived;
        public event Action<string, object>? CVarSyncReceived;

        public LogicThread(string serverAddress, IObjectTypeManager typeManager, IObjectFactory objectFactory, ISnapshotManager snapshotManager, IStateInterpolator stateInterpolator)
        {
            CurrentState = new GameState();
            _snapshotManager = snapshotManager;
            _stateInterpolator = stateInterpolator;
            _listener = new EventBasedNetListener();
            _netManager = new NetManager(_listener);
            _thread = new Thread(GameLoop);
            _serverAddress = serverAddress;
            _typeManager = typeManager;
            _objectFactory = objectFactory;
            _binaryService = new Shared.Services.BinarySnapshotService(new Shared.Services.BitPackedSnapshotSerializer());

            _listener.NetworkReceiveEvent += OnNetworkReceive;
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
            if (_netManager.FirstPeer != null && _netManager.FirstPeer.ConnectionState == ConnectionState.Connected)
            {
                var writer = new LiteNetLib.Utils.NetDataWriter();
                writer.Put(command);
                _netManager.FirstPeer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            var messageType = (SnapshotMessageType)reader.GetByte();

            if (messageType == SnapshotMessageType.BitPackedDelta || messageType == SnapshotMessageType.Binary)
            {
                byte[] data = new byte[reader.AvailableBytes];
                reader.GetBytes(data, reader.AvailableBytes);

                lock (_lock)
                {
                    if (messageType == SnapshotMessageType.BitPackedDelta)
                        _binaryService.DeserializeBitPacked(data, CurrentState.GameObjects, _typeManager, _objectFactory);
                    else
                        _binaryService.Deserialize(data, CurrentState.GameObjects, _typeManager, _objectFactory);

                    _snapshotManager.AddSnapshot(_gameTime.Elapsed.TotalSeconds, CurrentState.GameObjects.Values);
                }
            }
            else if (messageType == SnapshotMessageType.Sound)
            {
                var sound = new SoundData();
                sound.File = reader.GetString();
                sound.Volume = reader.GetFloat();
                sound.Pitch = reader.GetFloat();
                sound.Repeat = reader.GetBool();
                if (reader.GetBool()) sound.X = reader.GetLong();
                if (reader.GetBool()) sound.Y = reader.GetLong();
                if (reader.GetBool()) sound.Z = reader.GetLong();
                if (reader.GetBool()) sound.ObjectId = reader.GetLong();
                sound.Falloff = reader.GetFloat();
                SoundReceived?.Invoke(sound);
            }
            else if (messageType == SnapshotMessageType.StopSound)
            {
                var file = reader.GetString();
                long? objectId = null;
                if (reader.GetBool()) objectId = reader.GetLong();
                StopSoundReceived?.Invoke(file, objectId);
            }
            else if (messageType == SnapshotMessageType.SyncCVars)
            {
                var json = reader.GetString();
                var cvars = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (cvars != null)
                {
                    foreach (var kvp in cvars) CVarSyncReceived?.Invoke(kvp.Key, kvp.Value);
                }
            }

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
