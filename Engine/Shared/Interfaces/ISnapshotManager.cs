using System;
using System.Collections.Generic;
using Shared;
using Shared.Models;

namespace Shared.Interfaces;

public interface ISnapshotManager
{
    void AddSnapshot(double timestamp, IEnumerable<IGameObject> objects);
    (Snapshot? From, Snapshot? To, double T) GetInterpolationData(double renderTime);
}
