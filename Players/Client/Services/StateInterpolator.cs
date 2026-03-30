using System;
using System.Collections.Generic;
using Shared;

namespace Client.Services;

public interface IStateInterpolator
{
    void Interpolate(GameState world, Snapshot from, Snapshot to, double t);
}

public interface IInterpolatedProperty
{
    void Interpolate(IGameObject obj, in ObjectState from, in ObjectState to, double t);
}

public class PositionProperty : IInterpolatedProperty
{
    public void Interpolate(IGameObject obj, in ObjectState from, in ObjectState to, double t)
    {
        double interpX = from.X + (to.X - from.X) * t;
        double interpY = from.Y + (to.Y - from.Y) * t;
        obj.PixelX = (interpX - to.X) * 32;
        obj.PixelY = (interpY - to.Y) * 32;
    }
}

public class AlphaProperty : IInterpolatedProperty
{
    public void Interpolate(IGameObject obj, in ObjectState from, in ObjectState to, double t)
    {
        obj.Alpha = from.Visuals.Alpha + (to.Visuals.Alpha - from.Visuals.Alpha) * t;
    }
}

public class LayerProperty : IInterpolatedProperty
{
    public void Interpolate(IGameObject obj, in ObjectState from, in ObjectState to, double t)
    {
        obj.Layer = from.Visuals.Layer + (to.Visuals.Layer - from.Visuals.Layer) * t;
    }
}

public class StateInterpolator : IStateInterpolator
{
    private readonly List<IInterpolatedProperty> _properties = new()
    {
        new PositionProperty(),
        new AlphaProperty(),
        new LayerProperty()
    };

    public void Interpolate(GameState world, Snapshot from, Snapshot to, double t)
    {
        foreach (var kvp in to.States)
        {
            if (world.GameObjects.TryGetValue(kvp.Key, out var obj))
            {
                if (from.States.TryGetValue(kvp.Key, out var fromState))
                {
                    foreach (var prop in _properties)
                    {
                        prop.Interpolate(obj, fromState, kvp.Value, t);
                    }
                }
            }
        }
    }
}
