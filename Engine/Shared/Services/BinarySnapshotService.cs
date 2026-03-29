using System;
using System.IO;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Utils;

namespace Shared.Services;
    public class BinarySnapshotService : EngineService, IShrinkable
    {
        private readonly StringInterner? _interner;
        private readonly ThreadLocal<Dictionary<long, (byte[] Buffer, int Length, long Version)>> _deltaCache = new(() => new Dictionary<long, (byte[] Buffer, int Length, long Version)>(), trackAllValues: true);

        public BinarySnapshotService(StringInterner? interner = null)
        {
            _interner = interner;
        }

        public void Shrink()
        {
            // Thread-safe iteration through all thread-local dictionaries
            foreach (var dict in _deltaCache.Values)
            {
                foreach (var item in dict.Values)
                {
                    ArrayPool<byte>.Shared.Return(item.Buffer);
                }
                dict.Clear();
            }
        }

        public int SerializeTo(Span<byte> destination, IEnumerable<IGameObject> objects, IDictionary<long, long>? lastVersions, out bool truncated)
        {
            int offset = 0;
            truncated = false;

            // Fast-path: detect common collection types to avoid IEnumerable overhead
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
                int length = array.Length;
                for (int i = 0; i < length; i++)
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
                offset += Utils.VarInt.Write(destination.Slice(offset), 0); // End of stream marker
            }

            return offset;
        }

        private struct ChangeCounter : GameObject.IChangeVisitor
        {
            public int Count;
            public void Visit(int index, in DreamValue value) => Count++;
        }

        private ref struct ChangeSerializer : GameObject.IChangeVisitor
        {
            public Span<byte> Destination;
            public int Offset;
            public bool Truncated;

            public void Visit(int propIdx, in DreamValue val)
            {
                if (Truncated) return;
                int valueSize = val.GetWriteSize();
                if (Offset + 5 + valueSize > Destination.Length)
                {
                    Truncated = true;
                    return;
                }

                Offset += Utils.VarInt.Write(Destination.Slice(Offset), propIdx);
                Offset += val.WriteTo(Destination.Slice(Offset));
            }
        }

        private bool SerializeObject(Span<byte> destination, IGameObject obj, IDictionary<long, long>? lastVersions, ref int offset, out bool truncated)
        {
            truncated = false;
            if (lastVersions != null && lastVersions.TryGetValue(obj.Id, out long lastVersion) && lastVersion == obj.Version)
            {
                return false;
            }

            // Delta Caching: Check if we've already serialized this object state in the current tick
            var cache = _deltaCache.Value!;
            if (cache.TryGetValue(obj.Id, out var cached) && cached.Version == obj.Version)
            {
                if (offset + cached.Length > destination.Length)
                {
                    truncated = true;
                    return true;
                }
                cached.Buffer.AsSpan(0, cached.Length).CopyTo(destination.Slice(offset));
                offset += cached.Length;
                if (lastVersions != null) lastVersions[obj.Id] = obj.Version;
                return false;
            }

            // Check if we have enough space for the basic object data
            // Estimate: 7 fields * 10 bytes max per VarInt = 70 bytes. Use 128 for safety.
            if (offset + 128 > destination.Length)
            {
                truncated = true;
                return true;
            }

            int startOffset = offset;
            int objectStartOffset = offset;
            offset += Utils.VarInt.Write(destination.Slice(offset), obj.Id);
            offset += Utils.VarInt.Write(destination.Slice(offset), obj.Version);
            offset += Utils.VarInt.Write(destination.Slice(offset), (long)(obj.ObjectType?.Id ?? -1));
            offset += Utils.VarInt.Write(destination.Slice(offset), obj.X);
            offset += Utils.VarInt.Write(destination.Slice(offset), obj.Y);
            offset += Utils.VarInt.Write(destination.Slice(offset), obj.Z);

            if (obj.ObjectType != null && obj is GameObject g)
            {
                var counter = new ChangeCounter();
                g.VisitChanges(ref counter);

                offset += Utils.VarInt.Write(destination.Slice(offset), counter.Count);

                if (counter.Count > 0)
                {
                    var serializer = new ChangeSerializer { Destination = destination, Offset = offset };
                    g.VisitChanges(ref serializer);

                    if (serializer.Truncated)
                    {
                        offset = startOffset;
                        truncated = true;
                        return true;
                    }
                    offset = serializer.Offset;
                }
            }
            else
            {
                offset += Utils.VarInt.Write(destination.Slice(offset), 0);
            }

            // Cache the result for reuse in the same tick
            int length = offset - objectStartOffset;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
            destination.Slice(objectStartOffset, length).CopyTo(buffer);
            if (cache.TryGetValue(obj.Id, out var oldEntry))
            {
                ArrayPool<byte>.Shared.Return(oldEntry.Buffer);
            }
            cache[obj.Id] = (buffer, length, obj.Version);

            if (lastVersions != null) lastVersions[obj.Id] = obj.Version;
            return false;
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

            // Second pass: Resolve references
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
    }
