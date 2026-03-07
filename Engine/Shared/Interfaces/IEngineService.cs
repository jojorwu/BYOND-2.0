using System.Threading;
using System.Threading.Tasks;

namespace Shared.Interfaces;

    /// <summary>
    /// Represents the current execution status of an engine service.
    /// </summary>
    public enum ServiceStatus
    {
        /// <summary> The service is not running. </summary>
        Stopped,
        /// <summary> The service is currently in its startup phase. </summary>
        Starting,
        /// <summary> The service is fully initialized and operational. </summary>
        Running,
        /// <summary> The service is currently shutting down. </summary>
        Stopping,
        /// <summary> The service encountered an unrecoverable error during startup or execution. </summary>
        Failed
    }

    /// <summary>
    /// Defines the core contract for a manageable engine service.
    /// Services have a lifecycle (Initialize, Start, Stop) and are managed by the EngineApplication.
    /// </summary>
    public interface IEngineService : IAsyncInitializable
    {
        /// <summary>
        /// Starts the service asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the startup process.</param>
        Task StartAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gracefully stops the service asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the shutdown process.</param>
        Task StopAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Priority of the service. Higher priority services start first and stop last.
        /// </summary>
        int Priority => 0;

        /// <summary>
        /// Whether the service is critical to the engine's operation.
        /// If a critical service fails to start, the engine will shut down.
        /// </summary>
        bool IsCritical => true;

        /// <summary>
        /// Gets the current status of the service.
        /// </summary>
        ServiceStatus Status { get; }
    }
