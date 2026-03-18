using System.Threading;
using System.Threading.Tasks;

namespace Shared.Interfaces;

/// <summary>
/// Provides hooks for services that need to perform actions at specific engine lifecycle stages.
/// </summary>
public interface IEngineLifecycle
{
    /// <summary>
    /// Called after all services have been initialized but before the engine starts ticking.
    /// </summary>
    Task PostInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Called when the engine has started running and is ready for the first tick.
    /// </summary>
    Task OnStartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Called before any services start their shutdown process.
    /// </summary>
    Task PreShutdownAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Called after all services have been stopped.
    /// </summary>
    Task PostShutdownAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
