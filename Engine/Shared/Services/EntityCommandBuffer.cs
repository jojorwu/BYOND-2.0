using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        private readonly IJobSystem? _jobSystem;
        private readonly ConcurrentBag<CommandBuffer> _commandBuffers = new();
        private readonly ThreadLocal<CommandBuffer> _localBuffer;

        // Reusable fields for grouping to minimize allocations during Playback
        private readonly Dictionary<IGameObject, int> _targetToLastCommand = new(128);
        private readonly List<int> _creationIndices = new(128);
        private readonly List<IGameObject> _uniqueTargets = new(128);
        private static readonly SharedPool<List<int>> _intListPool = new(() => new List<int>(64));

        public EntityCommandBuffer(IObjectFactory objectFactory, IComponentManager componentManager, IJobSystem? jobSystem = null)
        {
            _objectFactory = objectFactory;
            _jobSystem = jobSystem;
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
                            var target = cmd.Target;
                            if (_targetToLastCommand.TryGetValue(target, out int prevIdx))
                            {
                                cmd = cmd.WithPrevIndex(prevIdx);
                            }
                            else
                            {
                                cmd = cmd.WithPrevIndex(-1);
                                _uniqueTargets.Add(target);
                            }
                            _targetToLastCommand[target] = globalIdx;
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
                if (_uniqueTargets.Count > 64 && _jobSystem != null)
                {
                    _jobSystem.ForEachAsync(_uniqueTargets, target =>
                    {
                        int lastIdx = _targetToLastCommand[target];

                        // Use stack-allocated buffer for common short command chains
                        Span<int> stackIndices = stackalloc int[16];
                        int count = 0;
                        int walker = lastIdx;
                        while (walker != -1 && count < stackIndices.Length)
                        {
                            stackIndices[count++] = walker;
                            walker = allCommands[walker].PrevIndex;
                        }

                        if (walker == -1)
                        {
                            for (int j = count - 1; j >= 0; j--)
                            {
                                ExecuteCommand(target, in allCommands[stackIndices[j]]);
                            }
                        }
                        else
                        {
                            // Fallback for very long command chains
                            var indices = new List<int>(count * 2);
                            for (int i = 0; i < count; i++) indices.Add(stackIndices[i]);
                            while (walker != -1)
                            {
                                indices.Add(walker);
                                walker = allCommands[walker].PrevIndex;
                            }

                            for (int j = indices.Count - 1; j >= 0; j--)
                            {
                                ExecuteCommand(target, in allCommands[indices[j]]);
                            }
                        }
                    }).GetAwaiter().GetResult();
                }
                else if (_uniqueTargets.Count > 64)
                {
                    System.Threading.Tasks.Parallel.ForEach(_uniqueTargets, target =>
                    {
                        int lastIdx = _targetToLastCommand[target];

                        // Use stack-allocated buffer for common short command chains
                        Span<int> stackIndices = stackalloc int[16];
                        int count = 0;
                        int walker = lastIdx;
                        while (walker != -1 && count < stackIndices.Length)
                        {
                            stackIndices[count++] = walker;
                            walker = allCommands[walker].PrevIndex;
                        }

                        if (walker == -1)
                        {
                            for (int j = count - 1; j >= 0; j--)
                            {
                                ExecuteCommand(target, in allCommands[stackIndices[j]]);
                            }
                        }
                        else
                        {
                            // Fallback for very long command chains
                            var indices = new List<int>(count * 2);
                            for (int i = 0; i < count; i++) indices.Add(stackIndices[i]);
                            while (walker != -1)
                            {
                                indices.Add(walker);
                                walker = allCommands[walker].PrevIndex;
                            }

                            for (int j = indices.Count - 1; j >= 0; j--)
                            {
                                ExecuteCommand(target, in allCommands[indices[j]]);
                            }
                        }
                    });
                }
                else
                {
                    // Pre-allocate indices span to avoid CA2014 stackalloc in loop
                    Span<int> stackIndices = stackalloc int[16];

                    for (int i = 0; i < _uniqueTargets.Count; i++)
                    {
                        var target = _uniqueTargets[i];
                        int lastIdx = _targetToLastCommand[target];

                        int count = 0;
                        int walker = lastIdx;
                        while (walker != -1 && count < stackIndices.Length)
                        {
                            stackIndices[count++] = walker;
                            walker = allCommands[walker].PrevIndex;
                        }

                        if (walker == -1)
                        {
                            for (int j = count - 1; j >= 0; j--)
                            {
                                ExecuteCommand(target, in allCommands[stackIndices[j]]);
                            }
                        }
                        else
                        {
                            var pooledIndices = _intListPool.Rent();
                            try
                            {
                                for (int k = 0; k < count; k++) pooledIndices.Add(stackIndices[k]);
                                while (walker != -1)
                                {
                                    pooledIndices.Add(walker);
                                    walker = allCommands[walker].PrevIndex;
                                }

                                for (int j = pooledIndices.Count - 1; j >= 0; j--)
                                {
                                    ExecuteCommand(target, in allCommands[pooledIndices[j]]);
                                }
                            }
                            finally
                            {
                                _intListPool.Return(pooledIndices);
                            }
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

        private void ExecuteCommand(IGameObject target, [In] in Command cmd)
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
