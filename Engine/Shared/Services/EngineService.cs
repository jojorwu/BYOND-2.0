using System.Threading;
using System.Threading.Tasks;
using Shared.Interfaces;

namespace Shared.Services;
    /// <summary>
    /// Base class for engine services with built-in status tracking.
    /// </summary>
    public abstract class EngineService : IEngineService
    {
        /// <inheritdoc />
        public virtual string Name => GetType().Name;

        /// <inheritdoc />
        public virtual int Priority => 0;

        /// <inheritdoc />
        public virtual IEnumerable<string> Dependencies => System.Array.Empty<string>();

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
        public virtual Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            Status = ServiceStatus.Running;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public virtual Task StopAsync(CancellationToken cancellationToken)
        {
            Status = ServiceStatus.Stopped;
            return Task.CompletedTask;
        }

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
    }
