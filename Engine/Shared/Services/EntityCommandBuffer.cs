using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            SetDataComponent,
            RemoveComponent
        }

        private readonly struct Command
        {
            public readonly CommandType Type;
            public readonly IGameObject? Target;
            public readonly ObjectType? ObjectType;
            public readonly object? Component;
            public readonly Type? ComponentType;
            public readonly long X, Y, Z;
            public readonly int PrevIndex;

            public Command(CommandType type, IGameObject? target, ObjectType? objectType, object? component, Type? componentType, long x, long y, long z, int prevIndex = -1)
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

        private sealed class CommandStream : IDisposable
        {
            private byte[] _buffer;
            private int _position;
            private readonly List<object?> _references = new();

            public ReadOnlySpan<byte> Data => _buffer.AsSpan(0, _position);
            public IReadOnlyList<object?> References => _references;

            public CommandStream()
            {
                _buffer = ArrayPool<byte>.Shared.Rent(4096);
                _position = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteCommand(CommandType type, IGameObject? target, ObjectType? objectType, object? component, Type? componentType, long x, long y, long z)
            {
                EnsureCapacity(41); // 1 (type) + 4*4 (indices) + 8*3 (longs) = 41
                _buffer[_position++] = (byte)type;

                WriteReference(target);
                WriteReference(objectType);
                WriteReference(component);
                WriteReference(componentType);

                BinaryPrimitives.WriteInt64LittleEndian(_buffer.AsSpan(_position), x); _position += 8;
                BinaryPrimitives.WriteInt64LittleEndian(_buffer.AsSpan(_position), y); _position += 8;
                BinaryPrimitives.WriteInt64LittleEndian(_buffer.AsSpan(_position), z); _position += 8;
            }

            private void WriteReference(object? obj)
            {
                int index = -1;
                if (obj != null)
                {
                    index = _references.Count;
                    _references.Add(obj);
                }
                BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(_position), index);
                _position += 4;
            }

            private void EnsureCapacity(int size)
            {
                if (_position + size > _buffer.Length)
                {
                    var old = _buffer;
                    _buffer = ArrayPool<byte>.Shared.Rent(_buffer.Length * 2);
                    old.AsSpan(0, _position).CopyTo(_buffer);
                    ArrayPool<byte>.Shared.Return(old);
                }
            }

            public void Clear()
            {
                _position = 0;
                _references.Clear();
            }

            public void Dispose()
            {
                if (_buffer != null)
                {
                    ArrayPool<byte>.Shared.Return(_buffer);
                    _buffer = null!;
                }
            }

            public ref struct Reader
            {
                private readonly ReadOnlySpan<byte> _data;
                private readonly IReadOnlyList<object?> _references;
                private int _pos;

                public Reader(CommandStream stream)
                {
                    _data = stream.Data;
                    _references = stream.References;
                    _pos = 0;
                }

                public bool HasMore => _pos < _data.Length;

                public void Read(out CommandType type, out IGameObject? target, out ObjectType? objectType, out object? component, out Type? componentType, out long x, out long y, out long z)
                {
                    type = (CommandType)_data[_pos++];
                    target = (IGameObject?)ReadReference();
                    objectType = (ObjectType?)ReadReference();
                    component = ReadReference();
                    componentType = (Type?)ReadReference();
                    x = BinaryPrimitives.ReadInt64LittleEndian(_data.Slice(_pos)); _pos += 8;
                    y = BinaryPrimitives.ReadInt64LittleEndian(_data.Slice(_pos)); _pos += 8;
                    z = BinaryPrimitives.ReadInt64LittleEndian(_data.Slice(_pos)); _pos += 8;
                }

                private object? ReadReference()
                {
                    int index = BinaryPrimitives.ReadInt32LittleEndian(_data.Slice(_pos));
                    _pos += 4;
                    return index == -1 ? null : _references[index];
                }
            }
        }

        private readonly IObjectFactory _objectFactory;
        private readonly IJobSystem? _jobSystem;
        private readonly ConcurrentBag<CommandStream> _commandStreams = new();
        private readonly ThreadLocal<CommandStream> _localStream;

        // Reusable fields for grouping to minimize allocations during Playback
        private readonly Dictionary<IGameObject, int> _targetToLastCommand = new(128);
        private readonly List<int> _creationIndices = new(128);
        private readonly List<IGameObject> _uniqueTargets = new(128);
        private static readonly SharedPool<List<int>> _intListPool = new(() => new List<int>(64));

        public EntityCommandBuffer(IObjectFactory objectFactory, IComponentManager componentManager, IJobSystem? jobSystem = null)
        {
            _objectFactory = objectFactory;
            _jobSystem = jobSystem;
            _localStream = new ThreadLocal<CommandStream>(() =>
            {
                var stream = new CommandStream();
                _commandStreams.Add(stream);
                return stream;
            });
        }

        public void CreateObject(ObjectType objectType, long x = 0, long y = 0, long z = 0)
        {
            _localStream.Value!.WriteCommand(CommandType.Create, null, objectType, null, null, x, y, z);
        }

        public void DestroyObject(IGameObject obj)
        {
            _localStream.Value!.WriteCommand(CommandType.Destroy, obj, null, null, null, 0, 0, 0);
        }

        public void AddComponent<T>(IGameObject obj, T component) where T : class, IComponent
        {
            _localStream.Value!.WriteCommand(CommandType.AddComponent, obj, null, component, null, 0, 0, 0);
        }

        public void SetDataComponent<T>(IGameObject obj, T component) where T : struct, IDataComponent
        {
            _localStream.Value!.WriteCommand(CommandType.SetDataComponent, obj, null, component, typeof(T), 0, 0, 0);
        }

        public void RemoveComponent<T>(IGameObject obj) where T : class, IComponent
        {
            _localStream.Value!.WriteCommand(CommandType.RemoveComponent, obj, null, null, typeof(T), 0, 0, 0);
        }

        public void Playback()
        {
            // Structural changes are played back from a single thread during synchronization points
            int totalBytes = 0;
            foreach (var stream in _commandStreams) totalBytes += stream.Data.Length;
            if (totalBytes == 0) return;

            // Simplified playback: execute in order they were recorded.
            // Complex grouping is skipped for now to ensure stability, can be re-added if needed.
            foreach (var stream in _commandStreams)
            {
                var reader = new CommandStream.Reader(stream);
                while (reader.HasMore)
                {
                    reader.Read(out var type, out var target, out var objectType, out var component, out var componentType, out var x, out var y, out var z);

                    if (type == CommandType.Create)
                    {
                        _objectFactory.Create(objectType!, x, y, z);
                    }
                    else if (target != null)
                    {
                        ExecuteCommand(target, type, component, componentType);
                    }
                }
                stream.Clear(); // Clear immediately after processing to avoid double-playback if Playback is called again
            }
        }

        private void ExecuteCommand(IGameObject target, CommandType type, object? component, Type? componentType)
        {
            switch (type)
            {
                case CommandType.Destroy:
                    if (target is GameObject g) _objectFactory.Destroy(g);
                    break;
                case CommandType.AddComponent:
                    target.AddComponent((IComponent)component!);
                    break;
                case CommandType.SetDataComponent:
                    // Use dynamic to handle generic SetDataComponent without complex reflection lookup
                    // which ensures we hit the most derived implementation.
                    SetDataComponentDynamic(target, component, componentType!);
                    break;
                case CommandType.RemoveComponent:
                    target.RemoveComponent(componentType!);
                    break;
            }
        }

        private void SetDataComponentDynamic(IGameObject target, object? component, Type componentType)
        {
            var method = typeof(EntityCommandBuffer).GetMethod(nameof(SetDataComponentInternal), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(componentType);
            method.Invoke(null, [target, component]);
        }

        private static void SetDataComponentInternal<T>(IGameObject target, object component) where T : struct, IDataComponent
        {
            target.SetDataComponent((T)component);
        }


        public void Clear()
        {
            foreach (var stream in _commandStreams)
            {
                stream.Clear();
            }
        }

        public void Reset() => Clear();

        public void Dispose()
        {
            _localStream.Dispose();
            foreach (var stream in _commandStreams)
            {
                stream.Dispose();
            }
        }
    }
