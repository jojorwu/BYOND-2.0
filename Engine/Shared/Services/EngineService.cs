using System.Threading;
using System.Threading.Tasks;
using Shared.Interfaces;

namespace Shared.Services;
    public abstract class EngineService : IEngineService
    {
        public virtual int Priority => 0;

        private ServiceStatus _status = ServiceStatus.Stopped;
        public ServiceStatus Status => _status;

        public virtual Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public virtual async Task StartAsync(CancellationToken cancellationToken)
        {
            _status = ServiceStatus.Starting;
            try
            {
                await OnStartAsync(cancellationToken);
                _status = ServiceStatus.Running;
            }
            catch
            {
                _status = ServiceStatus.Failed;
                throw;
            }
        }

        public virtual async Task StopAsync(CancellationToken cancellationToken)
        {
            _status = ServiceStatus.Stopping;
            try
            {
                await OnStopAsync(cancellationToken);
                _status = ServiceStatus.Stopped;
            }
            catch
            {
                _status = ServiceStatus.Failed;
                throw;
            }
        }

        protected virtual Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected virtual Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
