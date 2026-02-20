using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services;
    public class EntityCommandBuffer : IEntityCommandBuffer, IPoolable, IDisposable
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
        private readonly ConcurrentBag<List<Command>> _commandLists = new();
        private readonly ThreadLocal<List<Command>> _localCommands;

        public EntityCommandBuffer(IObjectFactory objectFactory, IComponentManager componentManager)
        {
            _objectFactory = objectFactory;
            _localCommands = new ThreadLocal<List<Command>>(() =>
            {
                var list = new List<Command>();
                _commandLists.Add(list);
                return list;
            });
        }

        public void CreateObject(ObjectType objectType, int x = 0, int y = 0, int z = 0)
        {
            _localCommands.Value!.Add(new Command { Type = CommandType.Create, ObjectType = objectType, X = x, Y = y, Z = z });
        }

        public void DestroyObject(IGameObject obj)
        {
            _localCommands.Value!.Add(new Command { Type = CommandType.Destroy, Target = obj });
        }

        public void AddComponent<T>(IGameObject obj, T component) where T : class, IComponent
        {
            _localCommands.Value!.Add(new Command { Type = CommandType.AddComponent, Target = obj, Component = component });
        }

        public void RemoveComponent<T>(IGameObject obj) where T : class, IComponent
        {
            _localCommands.Value!.Add(new Command { Type = CommandType.RemoveComponent, Target = obj, ComponentType = typeof(T) });
        }

        public void Playback()
        {
            // Structural changes are played back from a single thread during synchronization points
            foreach (var list in _commandLists)
            {
                foreach (var command in list)
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
            }
            Clear();
        }

        public void Clear()
        {
            foreach (var list in _commandLists)
            {
                list.Clear();
            }
        }

        public void Reset() => Clear();

        public void Dispose()
        {
            _localCommands.Dispose();
        }
    }
