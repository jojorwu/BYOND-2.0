using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Robust.Shared.Maths;
using Shared.Interfaces;
using Shared.Attributes;

namespace Shared.Services;

[EngineService(typeof(IPathfindingService))]
public class PathfindingService : EngineService, IPathfindingService
{
    private readonly IJobSystem _jobSystem;
    private readonly IGameState _gameState;

    public PathfindingService(IJobSystem jobSystem, IGameState gameState)
    {
        _jobSystem = jobSystem;
        _gameState = gameState;
    }

    public async Task<List<Vector3l>?> FindPathAsync(Vector3l start, Vector3l end, int maxDepth = 1000)
    {
        var tcs = new TaskCompletionSource<List<Vector3l>?>();
        _jobSystem.Schedule(() =>
        {
            try
            {
                var result = CalculateAStar(start, end, maxDepth);
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return await tcs.Task;
    }

    private List<Vector3l>? CalculateAStar(Vector3l start, Vector3l end, int maxDepth)
    {
        if (start == end) return new List<Vector3l> { start };

        var openSet = new PriorityQueue<Vector3l, float>();
        var cameFrom = new Dictionary<Vector3l, Vector3l>();
        var gScore = new Dictionary<Vector3l, float>();
        var fScore = new Dictionary<Vector3l, float>();

        gScore[start] = 0;
        fScore[start] = Heuristic(start, end);
        openSet.Enqueue(start, fScore[start]);

        int iterations = 0;
        while (openSet.Count > 0 && iterations++ < maxDepth)
        {
            var current = openSet.Dequeue();
            if (current == end) return ReconstructPath(cameFrom, current);

            foreach (var neighbor in GetNeighbors(current))
            {
                float tentativeGScore = gScore[current] + 1;
                if (!gScore.TryGetValue(neighbor, out float score) || tentativeGScore < score)
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore + Heuristic(neighbor, end);
                    openSet.Enqueue(neighbor, fScore[neighbor]);
                }
            }
        }

        return null;
    }

    private float Heuristic(Vector3l a, Vector3l b)
    {
        return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }

    private IEnumerable<Vector3l> GetNeighbors(Vector3l pos)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                yield return new Vector3l(pos.X + dx, pos.Y + dy, pos.Z);
            }
        }
    }

    private List<Vector3l> ReconstructPath(Dictionary<Vector3l, Vector3l> cameFrom, Vector3l current)
    {
        var path = new List<Vector3l> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    public bool HasLineOfSight(Vector3l start, Vector3l end)
    {
        return true;
    }
}
