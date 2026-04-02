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
    private readonly List<INetworkFieldHandler> _fieldHandlers;

    public SnapshotManager(IEnumerable<INetworkFieldHandler> fieldHandlers)
    {
        _fieldHandlers = fieldHandlers.OrderBy(h => h.Priority).ToList();
    }

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

        if (snapshot.HandlerOffsets.Length != _fieldHandlers.Count)
             snapshot.HandlerOffsets = new int[_fieldHandlers.Count];

        int count = 0;
        if (objects is ICollection<IGameObject> coll) count = coll.Count;
        else count = objects.Count();

        int totalStride = _fieldHandlers.Sum(h => h.SnapshotStateSize);
        if (snapshot.ObjectIds.Length < count)
        {
            snapshot.ObjectIds = new long[count * 2];
            snapshot.StateBuffer = new byte[count * 2 * totalStride];
        }

        var tempObjects = System.Buffers.ArrayPool<IGameObject>.Shared.Rent(count);
        try
        {
            int idx = 0;
            foreach (var obj in objects) tempObjects[idx++] = obj;
            Array.Sort(tempObjects, 0, count, Comparer<IGameObject>.Create((a, b) => a.Id.CompareTo(b.Id)));

            snapshot.Count = count;
            int bufferOffset = 0;
            for (int hIdx = 0; hIdx < _fieldHandlers.Count; hIdx++)
            {
                var handler = _fieldHandlers[hIdx];
                snapshot.HandlerOffsets[hIdx] = bufferOffset;

                if (handler.SnapshotStateSize > 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        var obj = tempObjects[i];
                        if (hIdx == 0) snapshot.ObjectIds[i] = obj.Id;

                        handler.SaveState(snapshot.StateBuffer.AsSpan(bufferOffset + (i * handler.SnapshotStateSize), handler.SnapshotStateSize), obj);
                    }
                    bufferOffset += count * handler.SnapshotStateSize;
                }
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
