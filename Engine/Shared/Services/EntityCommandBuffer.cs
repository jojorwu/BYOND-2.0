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
            public int PrevIndex;
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

        // Reusable fields for grouping to minimize allocations during Playback
        private readonly Dictionary<IGameObject, int> _targetToLastCommand = new();
        private readonly List<int> _creationIndices = new();
        private readonly List<IGameObject> _uniqueTargets = new();

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
            int totalCount = 0;
            foreach (var list in _commandLists) totalCount += list.Count;
            if (totalCount == 0) return;

            var allCommands = ArrayPool<Command>.Shared.Rent(totalCount);
            try
            {
                int currentIdx = 0;
                _targetToLastCommand.Clear();
                _creationIndices.Clear();
                _uniqueTargets.Clear();

                foreach (var list in _commandLists)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var cmd = list.Commands[i];
                        int globalIdx = currentIdx++;

                        if (cmd.Type == CommandType.Create)
                        {
                            _creationIndices.Add(globalIdx);
                        }
                        else if (cmd.Target != null)
                        {
                            if (_targetToLastCommand.TryGetValue(cmd.Target, out int prevIdx))
                            {
                                cmd.PrevIndex = prevIdx;
                            }
                            else
                            {
                                cmd.PrevIndex = -1;
                                _uniqueTargets.Add(cmd.Target);
                            }
                            _targetToLastCommand[cmd.Target] = globalIdx;
                        }

                        allCommands[globalIdx] = cmd;
                    }
                }

                // Handle creations first
                for (int i = 0; i < _creationIndices.Count; i++)
                {
                    var cmd = allCommands[_creationIndices[i]];
                    _objectFactory.Create(cmd.ObjectType!, cmd.X, cmd.Y, cmd.Z);
                }

                // Handle grouped updates and destructions per entity to minimize structural transitions
                Span<int> entityCommandIndices = stackalloc int[16];
                for (int i = 0; i < _uniqueTargets.Count; i++)
                {
                    var target = _uniqueTargets[i];
                    int lastIdx = _targetToLastCommand[target];

                    // We need to execute commands in the order they were added, but our links go backwards.
                    // For a small number of commands per entity (usually 1-3), we can just use a small stack-allocated span or similar.
                    int entityCmdCount = 0;
                    int walker = lastIdx;
                    while (walker != -1 && entityCmdCount < 16)
                    {
                        entityCommandIndices[entityCmdCount++] = walker;
                        walker = allCommands[walker].PrevIndex;
                    }

                    // Execute in original order
                    for (int j = entityCmdCount - 1; j >= 0; j--)
                    {
                        var cmd = allCommands[entityCommandIndices[j]];
                        ExecuteCommand(target, cmd);
                    }

                    // If more than 16 commands, handle overflow (rare)
                    if (walker != -1)
                    {
                        var overflow = new List<int>();
                        while (walker != -1)
                        {
                            overflow.Add(walker);
                            walker = allCommands[walker].PrevIndex;
                        }
                        for (int j = overflow.Count - 1; j >= 0; j--)
                        {
                            var cmd = allCommands[overflow[j]];
                            ExecuteCommand(target, cmd);
                        }
                    }
                }
            }
            finally
            {
                ArrayPool<Command>.Shared.Return(allCommands);
            }

            Clear();
        }

        private void ExecuteCommand(IGameObject target, in Command cmd)
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
