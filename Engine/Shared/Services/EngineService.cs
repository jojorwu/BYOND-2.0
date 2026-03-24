using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Shared.Interfaces;
using Shared.Enums;
using Shared;

namespace Shared.Services;

/// <summary>
/// Base class for engine services with built-in status tracking and lifecycle hooks.
/// </summary>
public abstract class EngineService : IEngineService
{
    /// <inheritdoc />
    public virtual string Name => GetType().Name;

    /// <inheritdoc />
    public virtual int Priority => 0;

    /// <inheritdoc />
    public virtual IEnumerable<Type> Dependencies => System.Type.EmptyTypes;

    /// <inheritdoc />
    public virtual bool IsCritical => true;

    private ServiceStatus _status = ServiceStatus.Stopped;

    /// <inheritdoc />
    public virtual ServiceStatus Status
    {
        get => _status;
        protected set => _status = value;
    }

    /// <inheritdoc />
    public void SetStatus(ServiceStatus status)
    {
        Status = status;
    }

    /// <inheritdoc />
    public long InitializationDurationMs { get; protected set; }

    /// <inheritdoc />
    public long StartupDurationMs { get; protected set; }

    /// <inheritdoc />
    public void SetDurations(long initializationDurationMs, long startupDurationMs)
    {
        InitializationDurationMs = initializationDurationMs;
        StartupDurationMs = startupDurationMs;
    }

    /// <inheritdoc />
    public virtual async Task InitializeAsync()
    {
        var sw = Stopwatch.StartNew();
        SetStarting();
        await OnInitializeAsync();
        sw.Stop();
        InitializationDurationMs = sw.ElapsedMilliseconds;
    }

    /// <summary>
    /// Hook for service-specific initialization logic.
    /// </summary>
    protected virtual Task OnInitializeAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public virtual async Task StartAsync(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        await OnStartAsync(cancellationToken);
        Status = ServiceStatus.Running;
        sw.Stop();
        StartupDurationMs = sw.ElapsedMilliseconds;
    }

    /// <summary>
    /// Hook for service-specific startup logic.
    /// </summary>
    protected virtual Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual async Task StopAsync(CancellationToken cancellationToken)
    {
        SetStopping();
        await OnStopAsync(cancellationToken);
        Status = ServiceStatus.Stopped;
    }

    /// <summary>
    /// Hook for service-specific shutdown logic.
    /// </summary>
    protected virtual Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Marks the service as failed.
    /// </summary>
    protected void SetFailed()
    {
        Status = ServiceStatus.Failed;
    }

    /// <summary>
    /// Marks the service as starting.
    /// </summary>
    protected void SetStarting()
    {
        Status = ServiceStatus.Starting;
    }

    /// <summary>
    /// Marks the service as stopping.
    /// </summary>
    protected void SetStopping()
    {
        Status = ServiceStatus.Stopping;
    }

    /// <inheritdoc />
    public virtual Dictionary<string, object> GetDiagnosticInfo()
    {
        return new Dictionary<string, object>
        {
            { "Name", Name },
            { "Status", Status.ToString() },
            { "Priority", Priority },
            { "IsCritical", IsCritical },
            { "InitDuration", InitializationDurationMs },
            { "StartDuration", StartupDurationMs }
        };
    }

    /// <inheritdoc />
    public virtual Task<HealthResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var status = Status switch
        {
            ServiceStatus.Running => HealthStatus.Healthy,
            ServiceStatus.Failed => HealthStatus.Unhealthy,
            _ => HealthStatus.Degraded
        };

        return Task.FromResult(new HealthResult(status, $"Service is in {Status} state"));
    }
}
