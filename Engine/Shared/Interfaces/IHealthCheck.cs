using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Interfaces;

public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

public record HealthResult(HealthStatus Status, string? Description = null, Dictionary<string, object>? Data = null);

public interface IHealthCheck
{
    Task<HealthResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}
