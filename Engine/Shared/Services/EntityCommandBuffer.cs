using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

        private sealed class CommandBuffer : IDisposable
        {
            private Command[] _buffer;
            private int _count;
            public ReadOnlySpan<Command> Commands => _buffer.AsSpan(0, _count);
            public int Count => _count;

            public CommandBuffer()
            {
                _buffer = ArrayPool<Command>.Shared.Rent(1024);
                _count = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(in Command cmd)
            {
                if (_count == _buffer.Length)
                {
                    Expand();
                }
                _buffer[_count++] = cmd;
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private void Expand()
            {
                var oldBuffer = _buffer;
                _buffer = ArrayPool<Command>.Shared.Rent(oldBuffer.Length * 2);
                oldBuffer.AsSpan(0, _count).CopyTo(_buffer);
                ArrayPool<Command>.Shared.Return(oldBuffer);
            }

            public void Clear() => _count = 0;

            public void Dispose()
            {
                var buffer = _buffer;
                if (buffer != null)
                {
                    _buffer = null!;
                    ArrayPool<Command>.Shared.Return(buffer);
                }
            }
        }

        private readonly IObjectFactory _objectFactory;
        private readonly ConcurrentBag<CommandBuffer> _commandBuffers = new();
        private readonly ThreadLocal<CommandBuffer> _localBuffer;

        // Reusable fields for grouping to minimize allocations during Playback
        private readonly Dictionary<IGameObject, int> _targetToLastCommand = new(128);
        private readonly List<int> _creationIndices = new(128);
        private readonly List<IGameObject> _uniqueTargets = new(128);
        private static readonly SharedPool<List<int>> _intListPool = new(() => new List<int>(64));

        public EntityCommandBuffer(IObjectFactory objectFactory, IComponentManager componentManager)
        {
            _objectFactory = objectFactory;
            _localBuffer = new ThreadLocal<CommandBuffer>(() =>
            {
                var buffer = new CommandBuffer();
                _commandBuffers.Add(buffer);
                return buffer;
            });
        }

        public void CreateObject(ObjectType objectType, long x = 0, long y = 0, long z = 0)
        {
            var cmd = new Command(CommandType.Create, null, objectType, null, null, x, y, z);
            _localBuffer.Value!.Add(in cmd);
        }

        public void DestroyObject(IGameObject obj)
        {
            var cmd = new Command(CommandType.Destroy, obj, null, null, null, 0, 0, 0);
            _localBuffer.Value!.Add(in cmd);
        }

        public void AddComponent<T>(IGameObject obj, T component) where T : class, IComponent
        {
            var cmd = new Command(CommandType.AddComponent, obj, null, component, null, 0, 0, 0);
            _localBuffer.Value!.Add(in cmd);
        }

        public void RemoveComponent<T>(IGameObject obj) where T : class, IComponent
        {
            var cmd = new Command(CommandType.RemoveComponent, obj, null, null, typeof(T), 0, 0, 0);
            _localBuffer.Value!.Add(in cmd);
        }

        public void Playback()
        {
            // Structural changes are played back from a single thread during synchronization points
            int totalCount = 0;
            foreach (var buffer in _commandBuffers) totalCount += buffer.Count;
            if (totalCount == 0) return;

            var allCommands = ArrayPool<Command>.Shared.Rent(totalCount);
            try
            {
                int currentIdx = 0;
                _targetToLastCommand.Clear();
                _creationIndices.Clear();
                _uniqueTargets.Clear();

                foreach (var buffer in _commandBuffers)
                {
                    var commands = buffer.Commands;
                    for (int i = 0; i < commands.Length; i++)
                    {
                        var cmd = commands[i];
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
            foreach (var buffer in _commandBuffers)
            {
                buffer.Clear();
            }
        }

        public void Reset() => Clear();

        public void Dispose()
        {
            _localBuffer.Dispose();
            foreach (var buffer in _commandBuffers)
            {
                buffer.Dispose();
            }
        }
    }
