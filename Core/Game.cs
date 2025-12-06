using Microsoft.Extensions.DependencyInjection;

namespace Core
{
    public class Game : IGame
    {
        private IServiceProvider _serviceProvider = null!;

        public IGameApi Api => _serviceProvider.GetRequiredService<IGameApi>();
        public IProject Project => _serviceProvider.GetRequiredService<IProject>();
        public IObjectTypeManager ObjectTypeManager => _serviceProvider.GetRequiredService<IObjectTypeManager>();
        public IDmmService DmmService => _serviceProvider.GetRequiredService<IDmmService>();

        public void LoadProject(string projectPath)
        {
            var services = new ServiceCollection();
            ConfigureServices(services, projectPath);
            _serviceProvider = services.BuildServiceProvider();

            var objectTypeManager = _serviceProvider.GetRequiredService<IObjectTypeManager>();
            var wall = new ObjectType("wall");
            wall.DefaultProperties["SpritePath"] = "assets/wall.png";
            objectTypeManager.RegisterObjectType(wall);
            var floor = new ObjectType("floor");
            floor.DefaultProperties["SpritePath"] = "assets/floor.png";
            objectTypeManager.RegisterObjectType(floor);
        }

        private void ConfigureServices(IServiceCollection services, string projectPath)
        {
            services.AddSingleton<IProject>(new Project(projectPath));
            services.AddSingleton<GameState>();
            services.AddSingleton<IObjectTypeManager, ObjectTypeManager>();
            services.AddSingleton<MapLoader>();
            services.AddSingleton<IMapApi, MapApi>();
            services.AddSingleton<IObjectApi, ObjectApi>();
            services.AddSingleton<IScriptApi, ScriptApi>();
            services.AddSingleton<IStandardLibraryApi, StandardLibraryApi>();
            services.AddSingleton<IGameApi, GameApi>();
            services.AddSingleton<OpenDreamCompilerService>();
            services.AddSingleton<IDmmService, DmmService>();
        }
    }
}
