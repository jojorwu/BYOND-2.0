using System;
using System.IO;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Shared.Interfaces;
using Shared.Utils;
using Shared.Models;
using Shared.Attributes;

using Shared.Buffers;
namespace Shared.Services;

[EngineService]
public class BinarySnapshotService : EngineService, IShrinkable
{
    private readonly StringInterner? _interner;
    private readonly ISnapshotSerializer _serializer;
    private readonly ThreadLocal<Dictionary<long, (long BufferOffset, int Length, long Version)>> _deltaCache = new(() => new Dictionary<long, (long BufferOffset, int Length, long Version)>(), trackAllValues: true);
    private readonly SnapshotBuffer _buffer = new();
    private long _bufferVersion = 1;

    public BinarySnapshotService(ISnapshotSerializer serializer, StringInterner? interner = null)
    {
        _serializer = serializer;
        _interner = interner;
    }

    public void ResetBuffer()
    {
        _buffer.Reset();
        _bufferVersion++;
        foreach (var dict in _deltaCache.Values)
        {
            dict.Clear();
        }
    }

    public void Shrink() => ResetBuffer();

    public int SerializeTo(Span<byte> destination, IEnumerable<IGameObject> objects, IDictionary<long, long>? lastVersions, out bool truncated)
    {
        int offset = 0;
        truncated = false;

        if (objects is IReadOnlyList<IGameObject> list)
        {
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                if (SerializeObject(destination, list[i], lastVersions, ref offset, out truncated)) break;
            }
        }
        else if (objects is IGameObject[] array)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (SerializeObject(destination, array[i], lastVersions, ref offset, out truncated)) break;
            }
        }
        else
        {
            foreach (var obj in objects)
            {
                if (SerializeObject(destination, obj, lastVersions, ref offset, out truncated)) break;
            }
        }

        if (offset < destination.Length)
        {
            offset += Utils.VarInt.Write(destination.Slice(offset), 0);
        }

        return offset;
    }

    public void SerializeBitPackedDelta(ref BitWriter writer, IEnumerable<IGameObject> objects, IDictionary<long, long>? lastVersions)
    {
        _serializer.SerializeBitPackedDelta(ref writer, objects, lastVersions);
    }

    public int SerializeBatches(Span<byte> destination, IEnumerable<ReactiveStateSystem.DeltaBatch> batches, out bool truncated)
    {
        int offset = 0;
        truncated = false;

        foreach (var batch in batches)
        {
            if (offset + 128 > destination.Length) { truncated = true; break; }

            offset += Utils.VarInt.Write(destination.Slice(offset), batch.EntityId);
            offset += Utils.VarInt.Write(destination.Slice(offset), 0);

            var changes = batch.Changes;
            offset += Utils.VarInt.Write(destination.Slice(offset), changes.Count);

            for (int i = 0; i < changes.Count; i++)
            {
                var change = changes[i];
                int writeSize = change.Value.GetWriteSize();
                if (offset + 10 + writeSize > destination.Length) { truncated = true; break; }

                offset += Utils.VarInt.Write(destination.Slice(offset), change.Index);
                offset += change.Value.WriteTo(destination.Slice(offset));
            }
            if (truncated) break;
        }

        if (offset < destination.Length)
        {
            offset += Utils.VarInt.Write(destination.Slice(offset), 0);
        }

        return offset;
    }

    private ref struct SinglePassSerializer : GameObject.IChangeVisitor
    {
        public Span<byte> PropertyBuffer;
        public int Offset;
        public int Count;
        public bool Truncated;

        public void Visit(int propIdx, in DreamValue val)
        {
            if (Truncated) return;
            int valueSize = val.GetWriteSize();
            if (Offset + 5 + valueSize > PropertyBuffer.Length)
            {
                Truncated = true;
                return;
            }

            Offset += Utils.VarInt.Write(PropertyBuffer.Slice(Offset), propIdx);
            Offset += val.WriteTo(PropertyBuffer.Slice(Offset));
            Count++;
        }
    }

    private bool SerializeObject(Span<byte> destination, IGameObject obj, IDictionary<long, long>? lastVersions, ref int offset, out bool truncated)
    {
        truncated = false;
        if (lastVersions != null && lastVersions.TryGetValue(obj.Id, out long lastVersion) && lastVersion == obj.Version)
        {
            return false;
        }

        var cache = _deltaCache.Value!;
        if (cache.TryGetValue(obj.Id, out var cached) && cached.Version == obj.Version)
        {
            if (offset + cached.Length > destination.Length)
            {
                truncated = true;
                return true;
            }
            _buffer.GetSegmentAsSpan(cached.BufferOffset, cached.Length).CopyTo(destination.Slice(offset));
            offset += cached.Length;
            if (lastVersions != null) lastVersions[obj.Id] = obj.Version;
            return false;
        }

        long slabOffset;
        var slabSpan = _buffer.AcquireSegment(2048, out slabOffset);

        int localOffset = 0;
        localOffset += Utils.VarInt.Write(slabSpan.Slice(localOffset), obj.Id);
        localOffset += Utils.VarInt.Write(slabSpan.Slice(localOffset), obj.Version);
        localOffset += Utils.VarInt.Write(slabSpan.Slice(localOffset), (long)(obj.ObjectType?.Id ?? -1));
        localOffset += Utils.VarInt.Write(slabSpan.Slice(localOffset), obj.X);
        localOffset += Utils.VarInt.Write(slabSpan.Slice(localOffset), obj.Y);
        localOffset += Utils.VarInt.Write(slabSpan.Slice(localOffset), obj.Z);

        if (obj.ObjectType != null && obj is GameObject g)
        {
            Span<byte> propBuffer = stackalloc byte[1024];
            var serializer = new SinglePassSerializer { PropertyBuffer = propBuffer };
            g.VisitChanges(ref serializer);

            if (serializer.Truncated)
            {
                byte[] largeBuffer = ArrayPool<byte>.Shared.Rent(4096);
                try {
                    serializer = new SinglePassSerializer { PropertyBuffer = largeBuffer };
                    g.VisitChanges(ref serializer);
                    WriteProperties(ref localOffset, slabSpan, serializer);
                } finally {
                    ArrayPool<byte>.Shared.Return(largeBuffer);
                }
            }
            else
            {
                WriteProperties(ref localOffset, slabSpan, serializer);
            }
        }
        else
        {
            localOffset += Utils.VarInt.Write(slabSpan.Slice(localOffset), 0);
        }

        cache[obj.Id] = (slabOffset, localOffset, obj.Version);

        if (offset + localOffset > destination.Length)
        {
            truncated = true;
            return true;
        }
        slabSpan.Slice(0, localOffset).CopyTo(destination.Slice(offset));
        offset += localOffset;

        if (lastVersions != null) lastVersions[obj.Id] = obj.Version;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteProperties(ref int localOffset, Span<byte> slabSpan, in SinglePassSerializer serializer)
    {
        localOffset += Utils.VarInt.Write(slabSpan.Slice(localOffset), serializer.Count);
        serializer.PropertyBuffer.Slice(0, serializer.Offset).CopyTo(slabSpan.Slice(localOffset));
        localOffset += serializer.Offset;
    }

    public void Dispose()
    {
        _buffer.Dispose();
        _deltaCache.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Deserialize(byte[] data, IDictionary<long, GameObject> world, IObjectTypeManager typeManager, IObjectFactory factory)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        ReadOnlySpan<byte> span = data;
        int offset = 0;
        var unresolvedReferences = new List<(GameObject target, int propIdx, long refId)>();

        while (offset < span.Length)
        {
            if (offset + 1 > span.Length) break;

            long id = Utils.VarInt.Read(span.Slice(offset), out int idBytes);
            offset += idBytes;
            if (id == 0) break;

            GameObject.EnsureNextId(id);

            if (offset + 5 > span.Length) throw new InvalidDataException("Unexpected end of stream during object header");

            long version = Utils.VarInt.Read(span.Slice(offset), out int vBytes);
            offset += vBytes;
            long typeId = Utils.VarInt.Read(span.Slice(offset), out int tBytes);
            offset += tBytes;

            long x = Utils.VarInt.Read(span.Slice(offset), out int xBytes);
            offset += xBytes;
            long y = Utils.VarInt.Read(span.Slice(offset), out int yBytes);
            offset += yBytes;
            long z = Utils.VarInt.Read(span.Slice(offset), out int zBytes);
            offset += zBytes;

            bool skip = false;
            if (world.TryGetValue(id, out var gameObject))
            {
                if (gameObject.Version >= version) skip = true;
            }
            else
            {
                var type = typeManager.GetObjectType((int)typeId);
                if (type != null)
                {
                    gameObject = factory.Create(type, x, y, z);
                    gameObject.Id = id;
                    world[id] = gameObject;
                }
                else
                {
                    skip = true;
                }
            }

            if (!skip && gameObject != null)
            {
                gameObject.SetPosition(x, y, z);
            }

            if (offset + 1 > span.Length) throw new InvalidDataException("Unexpected end of stream before property count");

            int propertyCount = (int)Utils.VarInt.Read(span.Slice(offset), out int propCountBytes);
            offset += propCountBytes;
            for (int i = 0; i < propertyCount; i++)
            {
                if (offset + 1 > span.Length) throw new InvalidDataException("Unexpected end of stream during property deserialization");

                int propIdx = (int)Utils.VarInt.Read(span.Slice(offset), out int propIdxBytes);
                offset += propIdxBytes;

                if (offset + 1 > span.Length) throw new InvalidDataException("Unexpected end of stream before property value");
                var val = DreamValue.ReadFrom(span.Slice(offset), out int valBytes);
                offset += valBytes;

                if (!skip && gameObject != null)
                {
                    if (val.IsObjectIdReference)
                    {
                        unresolvedReferences.Add((gameObject, propIdx, val.ObjectId));
                    }
                    else
                    {
                        gameObject.SetVariableDirect(propIdx, val);
                    }
                }
            }

            if (!skip && gameObject != null)
            {
                gameObject.Version = version;
            }
        }

        foreach (var (target, propIdx, refId) in unresolvedReferences)
        {
            if (world.TryGetValue(refId, out var refObj))
            {
                target.SetVariableDirect(propIdx, new DreamValue(refObj));
            }
            else
            {
                target.SetVariableDirect(propIdx, DreamValue.Null);
            }
        }
    }

    public void DeserializeBitPacked(ref BitReader reader, IDictionary<long, GameObject> world, IObjectTypeManager typeManager, IObjectFactory factory)
    {
        _serializer.DeserializeBitPacked(ref reader, world, typeManager, factory);
    }
}
