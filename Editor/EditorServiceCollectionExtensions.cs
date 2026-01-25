using DMCompiler;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace Editor
{
    public static class EditorServiceCollectionExtensions
    {
        public static IServiceCollection AddEditorCompilerServices(this IServiceCollection services)
        {
            services.AddSingleton<ICompilerService, OpenDreamCompilerService>();
            services.AddSingleton<IDmmParserService, DMMParserService>();
            return services;
        }
    }
}
