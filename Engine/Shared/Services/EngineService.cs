using System.Threading;
using System.Threading.Tasks;
using Shared.Interfaces;

namespace Shared.Services;
    public abstract class EngineService : IEngineService
    {
        public virtual int Priority => 0;

        public virtual Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public virtual Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
