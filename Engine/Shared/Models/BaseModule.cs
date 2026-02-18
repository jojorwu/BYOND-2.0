using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shared.Interfaces;

namespace Shared.Models
{
    public abstract class BaseModule : IEngineModule
    {
        public virtual string Name => GetType().Name;

        public virtual void RegisterServices(IServiceCollection services) { }

        public virtual Task InitializeAsync(IServiceProvider serviceProvider) => Task.CompletedTask;

        public virtual Task ShutdownAsync() => Task.CompletedTask;
    }
}
