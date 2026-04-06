using System;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Models;
using Shared.Enums;
using Shared.Attributes;

namespace Shared.Services;

[EngineService(typeof(IStateInterpolator))]
public class InterpolationService : IStateInterpolator
{
    private readonly List<INetworkFieldHandler> _fieldHandlers;

    public InterpolationService(IEnumerable<INetworkFieldHandler> fieldHandlers)
    {
        _fieldHandlers = fieldHandlers.OrderBy(h => h.Priority).ToList();
    }

    public void Interpolate(GameState world, Snapshot from, Snapshot to, double t)
    {
        for (int hIdx = 0; hIdx < _fieldHandlers.Count; hIdx++)
        {
            var handler = _fieldHandlers[hIdx];
            int size = handler.SnapshotStateSize;
            if (size == 0) continue;

            int toBaseOffset = to.HandlerOffsets[hIdx];
            bool hasFromOffsets = from.HandlerOffsets.Length > hIdx;
            int fromBaseOffset = hasFromOffsets ? from.HandlerOffsets[hIdx] : -1;

            for (int i = 0; i < to.Count; i++)
            {
                long id = to.ObjectIds[i];
                if (world.GameObjects.TryGetValue(id, out var obj))
                {
                    var toSpan = to.StateBuffer.AsSpan(toBaseOffset + (i * size), size);

                    int fromIdx = fromBaseOffset != -1 ? Array.BinarySearch(from.ObjectIds, 0, from.Count, id) : -1;
                    if (fromIdx >= 0)
                    {
                        var fromSpan = from.StateBuffer.AsSpan(fromBaseOffset + (fromIdx * size), size);
                        handler.Interpolate(obj, fromSpan, toSpan, t);
                    }
                    else
                    {
                        // No 'from' state for this object in this handler's slice
                        // For transform, we should snap.
                    }
                }
            }
        }
    }
}
