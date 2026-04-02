using System;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Models;
using Shared.Enums;

namespace Shared.Services;

public class InterpolationService : IStateInterpolator
{
    private readonly List<INetworkFieldHandler> _fieldHandlers;

    public InterpolationService(IEnumerable<INetworkFieldHandler> fieldHandlers)
    {
        _fieldHandlers = fieldHandlers.OrderBy(h => h.Priority).ToList();
    }

    public void Interpolate(GameState world, Snapshot from, Snapshot to, double t)
    {
        for (int i = 0; i < to.Count; i++)
        {
            long id = to.ObjectIds[i];
            var toSpan = to.GetStateSpan(id);

            if (world.GameObjects.TryGetValue(id, out var obj))
            {
                var fromSpan = from.GetStateSpan(id);
                if (!fromSpan.IsEmpty)
                {
                    int offset = 0;
                    foreach (var handler in _fieldHandlers)
                    {
                        if (handler.SnapshotStateSize > 0)
                        {
                            handler.Interpolate(obj, fromSpan.Slice(offset, handler.SnapshotStateSize), toSpan.Slice(offset, handler.SnapshotStateSize), t);
                            offset += handler.SnapshotStateSize;
                        }
                    }
                }
                else
                {
                    // Snap logic - ideally we'd have handler.SnapToState or similar
                    // But for now, we'll let TransformHandler handle it during normal interpolate if it was smarter
                }
            }
        }
    }
}
