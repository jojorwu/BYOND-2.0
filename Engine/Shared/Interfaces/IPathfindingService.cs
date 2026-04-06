using System.Collections.Generic;
using System.Threading.Tasks;
using Robust.Shared.Maths;

namespace Shared.Interfaces;

public interface IPathfindingService
{
    /// <summary>
    /// Finds a path between two coordinates asynchronously using the JobSystem.
    /// </summary>
    Task<List<Vector3l>?> FindPathAsync(Vector3l start, Vector3l end, int maxDepth = 1000);

    /// <summary>
    /// Checks if a direct path exists between two points.
    /// </summary>
    bool HasLineOfSight(Vector3l start, Vector3l end);
}
