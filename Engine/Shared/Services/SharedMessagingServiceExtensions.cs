using Shared.Models;
using Shared.Enums;
using Shared.Operations;
using Microsoft.Extensions.DependencyInjection;
using Shared.Interfaces;
using Shared.Services;

namespace Shared.Services
{
    public static class SharedMessagingServiceExtensions
    {
        public static IServiceCollection AddSharedMessagingServices(this IServiceCollection services)
        {
            services.AddSingleton<IEventBus, EventBus>();
            return services;
        }
    }
}
