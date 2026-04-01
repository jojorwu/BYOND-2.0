using System;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Models;
using Shared.Collections;
using System.Linq;

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

        int count = 0;
        if (objects is ICollection<IGameObject> coll) count = coll.Count;
        else count = objects.Count();

        if (snapshot.ObjectIds.Length < count)
        {
            snapshot.ObjectIds = new long[count * 2];
            snapshot.ObjectStates = new ObjectState[count * 2];
        }

        var tempObjects = System.Buffers.ArrayPool<IGameObject>.Shared.Rent(count);
        try
        {
            int idx = 0;
            foreach (var obj in objects) tempObjects[idx++] = obj;
            Array.Sort(tempObjects, 0, count, Comparer<IGameObject>.Create((a, b) => a.Id.CompareTo(b.Id)));

            snapshot.Count = count;
            for (int i = 0; i < count; i++)
            {
                var obj = tempObjects[i];
                snapshot.ObjectIds[i] = obj.Id;
                snapshot.ObjectStates[i] = new ObjectState {
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
        }
        finally
        {
            System.Buffers.ArrayPool<IGameObject>.Shared.Return(tempObjects);
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
