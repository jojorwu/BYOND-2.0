using System;
using System.Collections.Generic;

namespace Shared.Models;

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
