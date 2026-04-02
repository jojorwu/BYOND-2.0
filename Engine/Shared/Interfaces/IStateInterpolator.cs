using System;
using Shared;
using Shared.Models;

namespace Shared.Interfaces;

public interface IStateInterpolator
{
    void Interpolate(GameState world, Snapshot from, Snapshot to, double t);
}
