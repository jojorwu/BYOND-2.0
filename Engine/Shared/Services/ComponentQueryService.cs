using System;
using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;

namespace Shared.Services
{
    public interface IComponentQueryService
    {
        IEnumerable<IGameObject> Query<T>() where T : class, IComponent;
        IEnumerable<IGameObject> Query(params Type[] componentTypes);
    }

    public class ComponentQueryService : IComponentQueryService
    {
        private readonly IGameState _gameState;

        public ComponentQueryService(IGameState gameState)
        {
            _gameState = gameState;
        }

        public IEnumerable<IGameObject> Query<T>() where T : class, IComponent
        {
            return _gameState.GameObjects.Values.Where(obj => obj.GetComponent<T>() != null);
        }

        public IEnumerable<IGameObject> Query(params Type[] componentTypes)
        {
            return _gameState.GameObjects.Values.Where(obj =>
                componentTypes.All(type => obj.GetComponents().Any(c => type.IsAssignableFrom(c.GetType())))
            );
        }
    }
}
