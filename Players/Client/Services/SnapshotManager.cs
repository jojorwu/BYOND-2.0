using System;
using System.Collections.Generic;
using Shared;

namespace Client.Services;

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

public interface ISnapshotManager
{
    void AddSnapshot(double timestamp, IEnumerable<IGameObject> objects);
    (Snapshot? From, Snapshot? To, double T) GetInterpolationData(double renderTime);
}

public class SnapshotManager : ISnapshotManager
{
    private readonly Queue<Snapshot> _snapshotQueue = new();
    private readonly Stack<Snapshot> _snapshotPool = new();
    private const int MaxQueueSize = 20;

    public void AddSnapshot(double timestamp, IEnumerable<IGameObject> objects)
    {
        if (!_snapshotPool.TryPop(out var snapshot)) snapshot = new Snapshot();
        snapshot.Timestamp = timestamp;

        foreach (var obj in objects)
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
        while (_snapshotQueue.Count > MaxQueueSize)
        {
            var old = _snapshotQueue.Dequeue();
            old.Reset();
            _snapshotPool.Push(old);
        }
    }

    public (Snapshot? From, Snapshot? To, double T) GetInterpolationData(double renderTime)
    {
        if (_snapshotQueue.Count < 2) return (null, null, 0);

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
            return (from, to, Math.Clamp(t, 0, 1));
        }

        return (null, null, 0);
    }
}
