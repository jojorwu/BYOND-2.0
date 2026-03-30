using System;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services;

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
