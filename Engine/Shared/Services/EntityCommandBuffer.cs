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

        private readonly struct Command
        {
            public readonly CommandType Type;
            public readonly IGameObject? Target;
            public readonly ObjectType? ObjectType;
            public readonly IComponent? Component;
            public readonly Type? ComponentType;
            public readonly long X, Y, Z;
            public readonly int PrevIndex;

            public Command(CommandType type, IGameObject? target, ObjectType? objectType, IComponent? component, Type? componentType, long x, long y, long z, int prevIndex = -1)
            {
                Type = type;
                Target = target;
                ObjectType = objectType;
                Component = component;
                ComponentType = componentType;
                X = x;
                Y = y;
                Z = z;
                PrevIndex = prevIndex;
            }

            public Command WithPrevIndex(int prevIndex) => new(Type, Target, ObjectType, Component, ComponentType, X, Y, Z, prevIndex);
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
        private static readonly SharedPool<List<int>> _intListPool = new(() => new List<int>(64));

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
            _localCommands.Value!.Add(new Command(CommandType.Create, null, objectType, null, null, x, y, z));
        }

        public void DestroyObject(IGameObject obj)
        {
            _localCommands.Value!.Add(new Command(CommandType.Destroy, obj, null, null, null, 0, 0, 0));
        }

        public void AddComponent<T>(IGameObject obj, T component) where T : class, IComponent
        {
            _localCommands.Value!.Add(new Command(CommandType.AddComponent, obj, null, component, null, 0, 0, 0));
        }

        public void RemoveComponent<T>(IGameObject obj) where T : class, IComponent
        {
            _localCommands.Value!.Add(new Command(CommandType.RemoveComponent, obj, null, null, typeof(T), 0, 0, 0));
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
                                cmd = cmd.WithPrevIndex(prevIdx);
                            }
                            else
                            {
                                cmd = cmd.WithPrevIndex(-1);
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

                // Handle grouped updates and destructions per entity.
                // Parallelize across entities to maximize throughput while maintaining per-entity order.
                if (_uniqueTargets.Count > 64)
                {
                    System.Threading.Tasks.Parallel.ForEach(_uniqueTargets, target =>
                    {
                        int lastIdx = _targetToLastCommand[target];
                        var indices = new List<int>(); // Local list for parallel safety

                        int walker = lastIdx;
                        while (walker != -1)
                        {
                            indices.Add(walker);
                            walker = allCommands[walker].PrevIndex;
                        }

                        for (int j = indices.Count - 1; j >= 0; j--)
                        {
                            ExecuteCommand(target, allCommands[indices[j]]);
                        }
                    });
                }
                else
                {
                    var pooledIndices = _intListPool.Rent();
                    try
                    {
                        for (int i = 0; i < _uniqueTargets.Count; i++)
                        {
                            var target = _uniqueTargets[i];
                            int lastIdx = _targetToLastCommand[target];

                            pooledIndices.Clear();
                            int walker = lastIdx;
                            while (walker != -1)
                            {
                                pooledIndices.Add(walker);
                                walker = allCommands[walker].PrevIndex;
                            }

                            // Execute in original order (reverse of our linked list)
                            for (int j = pooledIndices.Count - 1; j >= 0; j--)
                            {
                                var cmd = allCommands[pooledIndices[j]];
                                ExecuteCommand(target, cmd);
                            }
                        }
                    }
                    finally
                    {
                        _intListPool.Return(pooledIndices);
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
