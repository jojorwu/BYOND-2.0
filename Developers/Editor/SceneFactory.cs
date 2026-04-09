using Shared;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Editor
{
    public interface ISceneFactory
    {
        Scene CreateScene(string filePath);
    }

    public class SceneFactory : ISceneFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public SceneFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Scene CreateScene(string filePath)
        {
            // Use ActivatorUtilities to create a new instance of GameState, ensuring each scene
            // has its own independent state while still satisfying its dependencies from DI.
            var gameState = ActivatorUtilities.CreateInstance<GameState>(_serviceProvider);
            return new Scene(filePath, gameState);
        }
    }
}
