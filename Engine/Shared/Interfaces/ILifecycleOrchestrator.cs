using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Interfaces;

/// <summary>
/// Orchestrates the engine lifecycle by managing service startup and shutdown.
/// </summary>
public interface ILifecycleOrchestrator
{
    IReadOnlyDictionary<string, ServiceStatus> ServiceHealth { get; }
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
