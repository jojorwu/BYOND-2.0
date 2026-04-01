using System;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Models;
using Shared.Enums;

namespace Shared.Services;

public class InterpolationService : IStateInterpolator
{
    private readonly IEnumerable<IInterpolatedProperty> _properties;

    public InterpolationService(IEnumerable<IInterpolatedProperty> properties)
    {
        _properties = properties;
    }

    public void Interpolate(GameState world, Snapshot from, Snapshot to, double t)
    {
        for (int i = 0; i < to.Count; i++)
        {
            long id = to.ObjectIds[i];
            var toState = to.ObjectStates[i];

            if (world.GameObjects.TryGetValue(id, out var obj))
            {
                if (from.TryGetState(id, out var fromState))
                {
                    foreach (var prop in _properties)
                    {
                        prop.Interpolate(obj, fromState, toState, t);
                    }
                }
                else
                {
                    // If no 'from' state, just snap to 'to'
                    obj.RenderX = toState.X;
                    obj.RenderY = toState.Y;
                    obj.RenderZ = toState.Z;
                    obj.Rotation = toState.Rotation;
                }
            }
        }
    }
}

public class PositionProperty : IInterpolatedProperty
{
    public void Interpolate(IGameObject obj, in ObjectState from, in ObjectState to, double t)
    {
        if (obj is GameObject g)
        {
            g.RenderState.X = from.X + (to.X - from.X) * t;
            g.RenderState.Y = from.Y + (to.Y - from.Y) * t;
            g.RenderState.Z = from.Z + (to.Z - from.Z) * t;

            g.RenderState.PixelX = (g.RenderState.X - to.X) * 32;
            g.RenderState.PixelY = (g.RenderState.Y - to.Y) * 32;
        }
    }
}

public class AlphaProperty : IInterpolatedProperty
{
    public void Interpolate(IGameObject obj, in ObjectState from, in ObjectState to, double t)
    {
        if (obj is GameObject g)
        {
            g.RenderState.Alpha = from.Visuals.Alpha + (to.Visuals.Alpha - from.Visuals.Alpha) * t;
        }
    }
}

public class LayerProperty : IInterpolatedProperty
{
    public void Interpolate(IGameObject obj, in ObjectState from, in ObjectState to, double t)
    {
        if (obj is GameObject g)
        {
            g.RenderState.Layer = from.Visuals.Layer + (to.Visuals.Layer - from.Visuals.Layer) * t;
        }
    }
}

public class RotationProperty : IInterpolatedProperty
{
    public void Interpolate(IGameObject obj, in ObjectState from, in ObjectState to, double t)
    {
        if (obj is GameObject g)
        {
            float diff = to.Rotation - from.Rotation;
            while (diff < -180) diff += 360;
            while (diff > 180) diff -= 360;
            g.RenderState.Rotation = from.Rotation + diff * (float)t;
        }
    }
}
