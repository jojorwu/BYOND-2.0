using Core.Scripting;
using Core.Scripting.CSharp;
using Core.Scripting.DM;
using Core.Scripting.LuaSystem;
using Core.VM.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Core
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddCoreServices(this IServiceCollection services)
        {
            services.AddSingleton<GameState>();
            services.AddScoped<ObjectTypeManager>();
            services.AddScoped<DreamVM>();
            services.AddScoped<MapLoader>();
            services.AddScoped<IMapApi, MapApi>();
            services.AddScoped<IObjectApi, ObjectApi>();
            services.AddScoped<IStandardLibraryApi, StandardLibraryApi>();
            services.AddScoped<GameApi>();

            services.AddScoped<IScriptSystem, CSharpSystem>();
            services.AddScoped<IScriptSystem, LuaSystem>();
            services.AddScoped<IScriptSystem, DmSystem>();

            services.AddScoped<ScriptManager>();

            return services;
        }
    }
}
