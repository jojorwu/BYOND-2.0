using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
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
            public long X, Y, Z;
        }

        private class CommandList : IDisposable
        {
            public Command[] Commands;
            public int Count;

            public CommandList()
            {
                Commands = ArrayPool<Command>.Shared.Rent(256);
                Count = 0;
            }

            public void Add(Command cmd)
            {
                if (Count == Commands.Length)
                {
                    var oldArr = Commands;
                    var newArr = ArrayPool<Command>.Shared.Rent(oldArr.Length * 2);
                    Array.Copy(oldArr, newArr, oldArr.Length);
                    Commands = newArr;
                    ArrayPool<Command>.Shared.Return(oldArr);
                }
                Commands[Count++] = cmd;
            }

            public void Clear()
            {
                Count = 0;
            }

            public void Dispose()
            {
                var arr = Commands;
                if (arr != null)
                {
                    Commands = null!;
                    ArrayPool<Command>.Shared.Return(arr);
                }
            }
        }

        private readonly IObjectFactory _objectFactory;
        private readonly ConcurrentBag<CommandList> _commandLists = new();
        private readonly ThreadLocal<CommandList> _localCommands;

        public EntityCommandBuffer(IObjectFactory objectFactory, IComponentManager componentManager)
        {
            _objectFactory = objectFactory;
            _localCommands = new ThreadLocal<CommandList>(() =>
            {
                var list = new CommandList();
                _commandLists.Add(list);
                return list;
            });
        }

        public void CreateObject(ObjectType objectType, long x = 0, long y = 0, long z = 0)
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
            // Fast-path: Group commands by Target to minimize structural archetype transitions
            var groupedCommands = new Dictionary<IGameObject, List<Command>>();
            var creationCommands = new List<Command>();

            foreach (var list in _commandLists)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var command = list.Commands[i];
                    if (command.Type == CommandType.Create)
                    {
                        creationCommands.Add(command);
                    }
                    else if (command.Target != null)
                    {
                        if (!groupedCommands.TryGetValue(command.Target, out var targetCommands))
                        {
                            targetCommands = new List<Command>();
                            groupedCommands[command.Target] = targetCommands;
                        }
                        targetCommands.Add(command);
                    }
                }
            }

            // Handle creations first
            foreach (var cmd in creationCommands)
            {
                _objectFactory.Create(cmd.ObjectType!, cmd.X, cmd.Y, cmd.Z);
            }

            // Handle grouped updates and destructions
            foreach (var kvp in groupedCommands)
            {
                var target = kvp.Key;
                var commands = kvp.Value;

                foreach (var cmd in commands)
                {
                    switch (cmd.Type)
                    {
                        case CommandType.Destroy:
                            if (target is GameObject g) _objectFactory.Destroy(g);
                            break;
                        case CommandType.AddComponent:
                            target.AddComponent(cmd.Component!);
                            break;
                        case CommandType.RemoveComponent:
                            target.RemoveComponent(cmd.ComponentType!);
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
            foreach (var list in _commandLists)
            {
                list.Dispose();
            }
        }
    }
