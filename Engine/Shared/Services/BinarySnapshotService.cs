using System;
using System.IO;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Utils;
using Shared.Models;

namespace Shared.Services;
    public class BinarySnapshotService : EngineService, IShrinkable
    {
        private readonly StringInterner? _interner;
        private readonly ThreadLocal<Dictionary<long, (int BufferOffset, int Length, long Version)>> _deltaCache = new(() => new Dictionary<long, (int BufferOffset, int Length, long Version)>(), trackAllValues: true);
        private readonly SnapshotBuffer _buffer = new();
        private long _bufferVersion = 1;

        [Flags]
        private enum GameObjectFields : ushort
        {
            None = 0,
            Position = 1 << 0,
            Visuals = 1 << 1,
            Variables = 1 << 2,
            Type = 1 << 3,
            NewObject = 1 << 4
        }

        public BinarySnapshotService(StringInterner? interner = null)
        {
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

        public void Shrink()
        {
            ResetBuffer();
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

        public int SerializeBitPackedDelta(Span<byte> destination, IEnumerable<IGameObject> objects, IDictionary<long, long>? lastVersions, out bool truncated)
        {
            truncated = false;

            var writer = new BitWriter(destination);

            foreach (var obj in objects)
            {
                bool isNew = lastVersions == null || !lastVersions.ContainsKey(obj.Id);
                if (!isNew && lastVersions != null && lastVersions.TryGetValue(obj.Id, out long lastVersion) && lastVersion == obj.Version)
                {
                    continue;
                }

                if (writer.BytesWritten + 128 > destination.Length) { truncated = true; break; }

                writer.WriteVarInt(obj.Id);
                writer.WriteVarInt(obj.Version);

                GameObjectFields fields = GameObjectFields.None;
                if (isNew) fields |= GameObjectFields.NewObject | GameObjectFields.Type;

                fields |= GameObjectFields.Position;
                fields |= GameObjectFields.Visuals;

                var g = obj as GameObject;
                int changeCount = 0;
                if (g != null)
                {
                    var counter = new ChangeCounter();
                    g.VisitChanges(ref counter);
                    changeCount = counter.Count;
                    if (changeCount > 0) fields |= GameObjectFields.Variables;
                }

                writer.WriteBits((ulong)fields, 8);

                if ((fields & GameObjectFields.Type) != 0)
                {
                    writer.WriteVarInt(obj.ObjectType?.Id ?? -1);
                }

                if ((fields & GameObjectFields.Position) != 0)
                {
                    writer.WriteZigZag(obj.X);
                    writer.WriteZigZag(obj.Y);
                    writer.WriteZigZag(obj.Z);
                }

                if ((fields & GameObjectFields.Visuals) != 0)
                {
                    if (g != null)
                    {
                        writer.WriteVarInt(g.Dir);
                        writer.WriteDouble(g.Alpha);
                        writer.WriteDouble(g.Layer);
                        WriteBitString(ref writer, g.Icon);
                        WriteBitString(ref writer, g.IconState);
                        WriteBitString(ref writer, g.Color);
                    }
                }

                if ((fields & GameObjectFields.Variables) != 0 && g != null)
                {
                    writer.WriteVarInt(changeCount);
                    var serializer = new BitChangeSerializer { Writer = writer };
                    g.VisitChanges(ref serializer);
                    writer = serializer.Writer;
                }

                if (lastVersions != null) lastVersions[obj.Id] = obj.Version;
            }

            if (!truncated)
            {
                writer.WriteVarInt(0); // End marker
            }

            return writer.BytesWritten;
        }

        private static void WriteBitString(ref BitWriter writer, string? s)
        {
            if (string.IsNullOrEmpty(s))
            {
                writer.WriteVarInt(0);
            }
            else
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(s);
                writer.WriteVarInt(bytes.Length);
                foreach (var b in bytes) writer.WriteBits(b, 8);
            }
        }

        private static string ReadBitString(ref BitReader reader)
        {
            int len = (int)reader.ReadVarInt();
            if (len == 0) return string.Empty;
            byte[] bytes = new byte[len];
            for (int i = 0; i < len; i++) bytes[i] = (byte)reader.ReadBits(8);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        public int SerializeBatches(Span<byte> destination, IEnumerable<ReactiveStateSystem.DeltaBatch> batches, out bool truncated)
        {
            int offset = 0;
            truncated = false;

            foreach (var batch in batches)
            {
                if (offset + 128 > destination.Length) { truncated = true; break; }

                offset += Utils.VarInt.Write(destination.Slice(offset), batch.EntityId);
                offset += Utils.VarInt.Write(destination.Slice(offset), 0); // Placeholder for version (incremental batches don't need full version check)

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

        private struct ChangeCounter : GameObject.IChangeVisitor
        {
            public int Count;
            public void Visit(int index, in DreamValue value) => Count++;
        }

        private ref struct BitChangeSerializer : GameObject.IChangeVisitor
        {
            public BitWriter Writer;
            public void Visit(int propIdx, in DreamValue val)
            {
                Writer.WriteVarInt(propIdx);
                val.BitWriteTo(ref Writer);
            }
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
                _buffer.GetSegmentAsSpan(cached.BufferOffset, cached.Length).CopyTo(destination.Slice(offset));
                offset += cached.Length;
                if (lastVersions != null) lastVersions[obj.Id] = obj.Version;
                return false;
            }

            // High-performance path: Serialize into a temporary segment in the persistent slab first
            // We use a safe estimate of 1024 bytes per object for the temporary write.
            int slabOffset;
            var slabSpan = _buffer.AcquireSegment(Math.Min(1024, _buffer.Capacity - _buffer.Position), out slabOffset);

            int localOffset = 0;
            localOffset += Utils.VarInt.Write(slabSpan.Slice(localOffset), obj.Id);
            localOffset += Utils.VarInt.Write(slabSpan.Slice(localOffset), obj.Version);
            localOffset += Utils.VarInt.Write(slabSpan.Slice(localOffset), (long)(obj.ObjectType?.Id ?? -1));
            localOffset += Utils.VarInt.Write(slabSpan.Slice(localOffset), obj.X);
            localOffset += Utils.VarInt.Write(slabSpan.Slice(localOffset), obj.Y);
            localOffset += Utils.VarInt.Write(slabSpan.Slice(localOffset), obj.Z);

            if (obj.ObjectType != null && obj is GameObject g)
            {
                var counter = new ChangeCounter();
                g.VisitChanges(ref counter);

                localOffset += Utils.VarInt.Write(slabSpan.Slice(localOffset), counter.Count);

                if (counter.Count > 0)
                {
                    var serializer = new ChangeSerializer { Destination = slabSpan, Offset = localOffset };
                    g.VisitChanges(ref serializer);

                    if (serializer.Truncated)
                    {
                        // Slab segment too small, fall back to safe but slower path or handle properly
                        // For now we assume 1024 is enough or the buffer throws on overflow
                        truncated = true;
                        return true;
                    }
                    localOffset = serializer.Offset;
                }
            }
            else
            {
                localOffset += Utils.VarInt.Write(slabSpan.Slice(localOffset), 0);
            }

            // Store in cache
            cache[obj.Id] = (slabOffset, localOffset, obj.Version);

            // Copy to destination
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

        public void DeserializeBitPacked(byte[] data, IDictionary<long, GameObject> world, IObjectTypeManager typeManager, IObjectFactory factory)
        {
            var reader = new BitReader(data);
            var unresolvedReferences = new List<(GameObject target, int propIdx, long refId)>();

            while (true)
            {
                long id = reader.ReadVarInt();
                if (id == 0) break;

                long version = reader.ReadVarInt();
                GameObjectFields fields = (GameObjectFields)reader.ReadBits(8);

                GameObject? gameObject;
                world.TryGetValue(id, out gameObject);

                if ((fields & GameObjectFields.Type) != 0)
                {
                    int typeId = (int)reader.ReadVarInt();
                    if (gameObject == null)
                    {
                        var type = typeManager.GetObjectType(typeId);
                        if (type != null)
                        {
                            gameObject = factory.Create(type, 0, 0, 0);
                            gameObject.Id = id;
                            world[id] = gameObject;
                        }
                    }
                }

                if ((fields & GameObjectFields.Position) != 0)
                {
                    long x = reader.ReadZigZag();
                    long y = reader.ReadZigZag();
                    long z = reader.ReadZigZag();
                    if (gameObject != null && (gameObject.Version < version || (fields & GameObjectFields.NewObject) != 0))
                    {
                        gameObject.SetPosition(x, y, z);
                    }
                }

                if ((fields & GameObjectFields.Visuals) != 0)
                {
                    int dir = (int)reader.ReadVarInt();
                    double alpha = reader.ReadDouble();
                    double layer = reader.ReadDouble();
                    string icon = ReadBitString(ref reader);
                    string iconState = ReadBitString(ref reader);
                    string color = ReadBitString(ref reader);

                    if (gameObject != null && (gameObject.Version < version || (fields & GameObjectFields.NewObject) != 0))
                    {
                        gameObject.Dir = dir;
                        gameObject.Alpha = alpha;
                        gameObject.Layer = layer;
                        gameObject.Icon = icon;
                        gameObject.IconState = iconState;
                        gameObject.Color = color;
                    }
                }

                if ((fields & GameObjectFields.Variables) != 0)
                {
                    int propertyCount = (int)reader.ReadVarInt();
                    for (int i = 0; i < propertyCount; i++)
                    {
                        int propIdx = (int)reader.ReadVarInt();
                        var val = DreamValue.BitReadFrom(ref reader);

                        if (gameObject != null && (gameObject.Version < version || (fields & GameObjectFields.NewObject) != 0))
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
                }

                if (gameObject != null)
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
