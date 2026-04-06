using System.Collections.Concurrent;
using Shared.Enums;
using Shared.Interfaces;
using Shared.Models;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;
using Shared.Attributes;

namespace Shared.Services.Systems;

[EngineService]
public class VisibilityCullingSystem : BaseSystem
{
    private readonly IProfilingService _profiling;
    private readonly ConcurrentDictionary<long, bool> _visibilityResults = new();

    public override string Name => "VisibilityCullingSystem";
    public override ExecutionPhase Phase => ExecutionPhase.Simulation;

    // Example view bounds (should be provided by some camera service)
    public long ViewMinX, ViewMinY, ViewMaxX, ViewMaxY;

    public VisibilityCullingSystem(IProfilingService profiling)
    {
        _profiling = profiling;
    }

    [Shared.Attributes.Query]
    private EntityQuery<IComponent> _visualQuery = null!;

    public override async ValueTask TickAsync<T>(ArchetypeChunk<T> chunk, IEntityCommandBuffer ecb)
    {
        using (_profiling.Measure("VisibilityCulling.ProcessChunk"))
        {
            var xs = chunk.XsSpan;
            var ys = chunk.YsSpan;
            var ids = chunk.EntityIdsSpan;
            int count = chunk.Count;

            int i = 0;
            if (Vector256.IsHardwareAccelerated && count >= 4)
            {
                var vMinX = Vector256.Create(ViewMinX);
                var vMinY = Vector256.Create(ViewMinY);
                var vMaxX = Vector256.Create(ViewMaxX);
                var vMaxY = Vector256.Create(ViewMaxY);

                for (; i <= count - 4; i += 4)
                {
                    var vx = Vector256.Create(xs[i], xs[i+1], xs[i+2], xs[i+3]);
                    var vy = Vector256.Create(ys[i], ys[i+1], ys[i+2], ys[i+3]);

                    var inBounds = Vector256.GreaterThanOrEqual(vx, vMinX) &
                                   Vector256.LessThanOrEqual(vx, vMaxX) &
                                   Vector256.GreaterThanOrEqual(vy, vMinY) &
                                   Vector256.LessThanOrEqual(vy, vMaxY);

                    uint mask = (uint)Vector256.ExtractMostSignificantBits(inBounds);
                    for (int j = 0; j < 4; j++)
                    {
                        bool visible = (mask & (1u << j)) != 0;
                        _visibilityResults[ids[i + j]] = visible;
                    }
                }
            }

            for (; i < count; i++)
            {
                bool visible = xs[i] >= ViewMinX && xs[i] <= ViewMaxX &&
                               ys[i] >= ViewMinY && ys[i] <= ViewMaxY;
                _visibilityResults[ids[i]] = visible;
            }
        }
    }

    public override void Tick(IEntityCommandBuffer ecb)
    {
        // Handled by TickAsync<T> chunks
    }

    public bool IsVisible(long entityId) => _visibilityResults.GetValueOrDefault(entityId, true);
}
