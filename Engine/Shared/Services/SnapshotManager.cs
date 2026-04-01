using System;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Models;
using Shared.Collections;

namespace Shared.Services;

public class SnapshotManager : ISnapshotManager
{
    private readonly RingBuffer<Snapshot> _snapshotQueue = new(20);
    private readonly Stack<Snapshot> _snapshotPool = new();

    public void AddSnapshot(double timestamp, IEnumerable<IGameObject> objects)
    {
        if (_snapshotQueue.Count == _snapshotQueue.Capacity)
        {
             var oldest = _snapshotQueue.PopOldest();
             oldest.Reset();
             _snapshotPool.Push(oldest);
        }

        if (!_snapshotPool.TryPop(out var snapshot)) snapshot = new Snapshot();
        snapshot.Timestamp = timestamp;

        foreach (var obj in objects)
        {
            snapshot.States[obj.Id] = new ObjectState {
                X = obj.X,
                Y = obj.Y,
                Z = obj.Z,
                Rotation = obj.Rotation,
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

        _snapshotQueue.Add(snapshot);
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
