using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services
{
    public class EntityCommandBuffer : IEntityCommandBuffer
    {
        private readonly IObjectFactory _objectFactory;
        private readonly IComponentManager _componentManager;
        private readonly ConcurrentQueue<Action> _commands = new();

        public EntityCommandBuffer(IObjectFactory objectFactory, IComponentManager componentManager)
        {
            _objectFactory = objectFactory;
            _componentManager = componentManager;
        }

        public void CreateObject(ObjectType objectType, int x = 0, int y = 0, int z = 0)
        {
            _commands.Enqueue(() => _objectFactory.Create(objectType, x, y, z));
        }

        public void DestroyObject(IGameObject obj)
        {
            if (obj is GameObject gameObj)
            {
                _commands.Enqueue(() => _objectFactory.Destroy(gameObj));
            }
        }

        public void AddComponent<T>(IGameObject obj, T component) where T : class, IComponent
        {
            _commands.Enqueue(() => obj.AddComponent(component));
        }

        public void RemoveComponent<T>(IGameObject obj) where T : class, IComponent
        {
            _commands.Enqueue(() => obj.RemoveComponent<T>());
        }

        public void Playback()
        {
            while (_commands.TryDequeue(out var command))
            {
                command();
            }
        }
    }
}
