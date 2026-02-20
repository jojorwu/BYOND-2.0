using Microsoft.Extensions.DependencyInjection;

namespace Shared.Interfaces;

public interface IEngineModule
{
    void RegisterServices(IServiceCollection services);
    void PreTick() { }
    void PostTick() { }
}
