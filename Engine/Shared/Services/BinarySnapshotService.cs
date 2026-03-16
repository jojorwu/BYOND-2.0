using System;
using System.IO;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using Shared.Interfaces;

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
                offset += WriteVarInt(destination.Slice(offset), 0); // End of stream marker
            }

            return offset;
        }

        private struct ChangeCounter : GameObject.IChangeVisitor
        {
            public int Count;
            public void Visit(int index, in DreamValue value) => Count++;
        }

        private ref struct ChangeSerializer
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

                Offset += WriteVarInt(Destination.Slice(Offset), propIdx);
                Offset += val.WriteTo(Destination.Slice(Offset));
            }
        }

        private static void SerializeChange(int propIdx, in DreamValue val, ref ChangeSerializer serializer)
        {
            serializer.Visit(propIdx, val);
        }

        public static int WriteVarInt(Span<byte> span, long value)
        {
            ulong v = (ulong)value;
            int count = 0;
            while (v >= 0x80)
            {
                if (count >= span.Length) return 0;
                span[count++] = (byte)(v | 0x80);
                v >>= 7;
            }
            if (count >= span.Length) return 0;
            span[count++] = (byte)v;
            return count;
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
            // Estimate: ID(5) + Version(5) + Type(5) + 3*Int(4) + PropCount(5) = 32 bytes
            if (offset + 32 > destination.Length)
            {
                truncated = true;
                return true;
            }

            int startOffset = offset;
            int objectStartOffset = offset;
            offset += WriteVarInt(destination.Slice(offset), obj.Id);
            offset += WriteVarInt(destination.Slice(offset), obj.Version);
            offset += WriteVarInt(destination.Slice(offset), (long)(obj.ObjectType?.Id ?? -1));
            offset += WriteVarInt(destination.Slice(offset), obj.X);
            offset += WriteVarInt(destination.Slice(offset), obj.Y);
            offset += WriteVarInt(destination.Slice(offset), obj.Z);

            if (obj.ObjectType != null && obj is GameObject g)
            {
                var counter = new ChangeCounter();
                g.VisitChanges(ref counter);

                offset += WriteVarInt(destination.Slice(offset), counter.Count);

                if (counter.Count > 0)
                {
                    var serializer = new ChangeSerializer { Destination = destination, Offset = offset };
                    g.VisitChangesRef(ref serializer, SerializeChange);

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
                offset += WriteVarInt(destination.Slice(offset), 0);
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

        [Obsolete("Use SerializeTo with rented buffer instead")]
        public byte[] Serialize(IEnumerable<IGameObject> objects, IDictionary<long, long>? lastVersions = null)
        {
            int bufferSize = 65536;
            while (true)
            {
                var rentedBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                try
                {
                    int bytesWritten = SerializeTo(rentedBuffer, objects, lastVersions, out bool truncated);
                    if (!truncated)
                    {
                        byte[] result = new byte[bytesWritten];
                        rentedBuffer.AsSpan(0, bytesWritten).CopyTo(result);
                        return result;
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
                }

                if (bufferSize >= 1024 * 1024 * 512) // 512MB safety limit
                    throw new Exception("World state too large to serialize into a single snapshot");

                bufferSize *= 2;
            }
        }



        public long ReadVarInt(ReadOnlySpan<byte> span, out int bytesRead)
        {
            long result = 0;
            int shift = 0;
            bytesRead = 0;
            while (bytesRead < span.Length)
            {
                byte b = span[bytesRead++];
                result |= (long)(b & 0x7f) << shift;
                if ((b & 0x80) == 0) return result;
                shift += 7;
                if (shift >= 70) throw new Exception("Malformed VarInt");
            }
            throw new Exception("Unexpected end of stream while reading VarInt");
        }

        public void Deserialize(byte[] data, IDictionary<long, GameObject> world, IObjectTypeManager typeManager, IObjectFactory factory)
        {
            ReadOnlySpan<byte> span = data;
            int offset = 0;
            var unresolvedReferences = new List<(GameObject target, int propIdx, long refId)>();

            while (offset < span.Length)
            {
                long id = ReadVarInt(span.Slice(offset), out int idBytes);
                offset += idBytes;
                if (id == 0) break;

                GameObject.EnsureNextId(id);

                long version = ReadVarInt(span.Slice(offset), out int vBytes);
                offset += vBytes;
                long typeId = ReadVarInt(span.Slice(offset), out int tBytes);
                offset += tBytes;

                long x = ReadVarInt(span.Slice(offset), out int xBytes);
                offset += xBytes;
                long y = ReadVarInt(span.Slice(offset), out int yBytes);
                offset += yBytes;
                long z = ReadVarInt(span.Slice(offset), out int zBytes);
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

                int propertyCount = (int)ReadVarInt(span.Slice(offset), out int propCountBytes);
                offset += propCountBytes;
                for (int i = 0; i < propertyCount; i++)
                {
                    int propIdx = (int)ReadVarInt(span.Slice(offset), out int propIdxBytes);
                    offset += propIdxBytes;
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
