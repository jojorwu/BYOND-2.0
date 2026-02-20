using Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using Shared.Interfaces;

namespace Shared.Models;

public abstract class BaseModule : IEngineModule
{
    public abstract string Name { get; }
    public abstract void RegisterServices(IServiceCollection services);
    public virtual void PreTick() { }
    public virtual void PostTick() { }
}
