using System;
using System.Collections.Generic;

namespace Shared.Models;

public class Snapshot
{
    public double Timestamp;
    public long[] ObjectIds = Array.Empty<long>();
    public ObjectState[] ObjectStates = Array.Empty<ObjectState>();
    public int Count;

    public void Reset()
    {
        Timestamp = 0;
        Count = 0;
    }

    public bool TryGetState(long id, out ObjectState state)
    {
        int index = Array.BinarySearch(ObjectIds, 0, Count, id);
        if (index >= 0)
        {
            state = ObjectStates[index];
            return true;
        }
        state = default;
        return false;
    }
}

public struct ObjectState
{
    public long X;
    public long Y;
    public long Z;
    public float Rotation;
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
