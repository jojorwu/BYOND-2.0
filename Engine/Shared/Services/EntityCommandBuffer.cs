using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services
{
    public class EntityCommandBuffer : IEntityCommandBuffer
    {
        private enum CommandType
        {
            Create,
            Destroy,
            AddComponent,
            RemoveComponent
        }

        private struct Command
        {
            public CommandType Type;
            public IGameObject? Target;
            public ObjectType? ObjectType;
            public IComponent? Component;
            public Type? ComponentType;
            public int X, Y, Z;
        }

        private readonly IObjectFactory _objectFactory;
        private readonly List<Command> _commands = new();

        public EntityCommandBuffer(IObjectFactory objectFactory, IComponentManager componentManager)
        {
            _objectFactory = objectFactory;
        }

        public void CreateObject(ObjectType objectType, int x = 0, int y = 0, int z = 0)
        {
            lock (_commands)
            {
                _commands.Add(new Command { Type = CommandType.Create, ObjectType = objectType, X = x, Y = y, Z = z });
            }
        }

        public void DestroyObject(IGameObject obj)
        {
            lock (_commands)
            {
                _commands.Add(new Command { Type = CommandType.Destroy, Target = obj });
            }
        }

        public void AddComponent<T>(IGameObject obj, T component) where T : class, IComponent
        {
            lock (_commands)
            {
                _commands.Add(new Command { Type = CommandType.AddComponent, Target = obj, Component = component });
            }
        }

        public void RemoveComponent<T>(IGameObject obj) where T : class, IComponent
        {
            lock (_commands)
            {
                _commands.Add(new Command { Type = CommandType.RemoveComponent, Target = obj, ComponentType = typeof(T) });
            }
        }

        public void Playback()
        {
            // Structural changes are usually played back from a single thread during synchronization points
            foreach (var command in _commands)
            {
                switch (command.Type)
                {
                    case CommandType.Create:
                        _objectFactory.Create(command.ObjectType!, command.X, command.Y, command.Z);
                        break;
                    case CommandType.Destroy:
                        if (command.Target is GameObject g) _objectFactory.Destroy(g);
                        break;
                    case CommandType.AddComponent:
                        command.Target!.AddComponent(command.Component!);
                        break;
                    case CommandType.RemoveComponent:
                        command.Target!.RemoveComponent(command.ComponentType!);
                        break;
                }
            }
            _commands.Clear();
        }
    }
}
