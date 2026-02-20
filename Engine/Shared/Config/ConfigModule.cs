using Microsoft.Extensions.DependencyInjection;
using Shared.Interfaces;
using Shared.Models;
using Shared.Config;

namespace Shared.Config;

public class ConfigModule : BaseModule
{
    public override string Name => "Config";

    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IEngineConfig, EngineConfig>();
    }
}
