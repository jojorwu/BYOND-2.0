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

namespace Client
{
    public class LogicThread
    {
        public class Snapshot
        {
            public double Timestamp;
            public Dictionary<long, ObjectState> States = new();

            public void Reset()
            {
                Timestamp = 0;
                States.Clear();
            }
        }

        public struct ObjectState
        {
            public long X;
            public long Y;
            public long Z;
            public VisualData Visuals;
        }

        public struct VisualData
        {
            public int Dir;
            public double Alpha;
            public double Layer;
            public string Icon;
            public string IconState;
            public string Color;
        }

        private readonly Queue<Snapshot> _snapshotQueue = new();
        private readonly Stack<Snapshot> _snapshotPool = new();
        public GameState CurrentState { get; private set; }

        private readonly object _lock = new object();
        private Thread _thread;
        private bool _isRunning;
        public const int TicksPerSecond = 30;
        public const float TimeStep = 1.0f / TicksPerSecond;
        private const double InterpolationDelay = 0.1; // 100ms buffer for smoothness

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

        public LogicThread(string serverAddress, IObjectTypeManager typeManager, IObjectFactory objectFactory)
        {
            CurrentState = new GameState();
            _listener = new EventBasedNetListener();
            _netManager = new NetManager(_listener);
            _thread = new Thread(GameLoop);
            _serverAddress = serverAddress;
            _typeManager = typeManager;
            _objectFactory = objectFactory;
            _binaryService = new Shared.Services.BinarySnapshotService();

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

                    // Acquire pooled snapshot or create new
                    if (!_snapshotPool.TryPop(out var snapshot)) snapshot = new Snapshot();
                    snapshot.Timestamp = _gameTime.Elapsed.TotalSeconds;

                    foreach (var obj in CurrentState.GameObjects.Values)
                    {
                        snapshot.States[obj.Id] = new ObjectState {
                            X = obj.X,
                            Y = obj.Y,
                            Z = obj.Z,
                            Visuals = new VisualData {
                                Dir = obj.Dir,
                                Alpha = obj.Alpha,
                                Layer = obj.Layer,
                                Icon = obj.Icon,
                                IconState = obj.IconState,
                                Color = obj.Color
                            }
                        };
                    }
                    _snapshotQueue.Enqueue(snapshot);

                    // Recycle old snapshots
                    while (_snapshotQueue.Count > 20)
                    {
                        var old = _snapshotQueue.Dequeue();
                        old.Reset();
                        _snapshotPool.Push(old);
                    }
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
                if (_snapshotQueue.Count < 2) return;

                Snapshot? from = null;
                Snapshot? to = null;

                foreach (var s in _snapshotQueue)
                {
                    if (s.Timestamp <= renderTime) from = s;
                    if (s.Timestamp > renderTime)
                    {
                        to = s;
                        break;
                    }
                }

                if (from != null && to != null)
                {
                    double t = (renderTime - from.Timestamp) / (to.Timestamp - from.Timestamp);
                    t = Math.Clamp(t, 0, 1);

                    foreach (var kvp in to.States)
                    {
                        if (CurrentState.GameObjects.TryGetValue(kvp.Key, out var obj))
                        {
                            if (from.States.TryGetValue(kvp.Key, out var fromState))
                            {
                                // Interpolate Position
                                double interpX = fromState.X + (kvp.Value.X - fromState.X) * t;
                                double interpY = fromState.Y + (kvp.Value.Y - fromState.Y) * t;
                                double interpZ = fromState.Z + (kvp.Value.Z - fromState.Z) * t;

                                // Sub-tile smoothing using Pixel offsets
                                obj.PixelX = (interpX - kvp.Value.X) * 32;
                                obj.PixelY = (interpY - kvp.Value.Y) * 32;
                                // For Z, if we had PixelZ, we'd use it. For now, we only have PixelX/Y.

                                // Interpolate Visuals
                                obj.Alpha = fromState.Visuals.Alpha + (kvp.Value.Visuals.Alpha - fromState.Visuals.Alpha) * t;
                                obj.Layer = fromState.Visuals.Layer + (kvp.Value.Visuals.Layer - fromState.Visuals.Layer) * t;
                            }
                        }
                    }
                }
            }
        }

        public GameState GetStateForRender()
        {
            return CurrentState;
        }
    }
}
