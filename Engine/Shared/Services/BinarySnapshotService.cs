using System;
using System.IO;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using Shared.Interfaces;

namespace Shared.Services;
    public class BinarySnapshotService
    {
        private readonly StringInterner? _interner;

        public BinarySnapshotService(StringInterner? interner = null)
        {
            _interner = interner;
        }

        public int SerializeTo(Span<byte> destination, IEnumerable<IGameObject> objects, IDictionary<long, long>? lastVersions, out bool truncated)
        {
            int offset = 0;
            truncated = false;

            foreach (var obj in objects)
            {
                if (lastVersions != null && lastVersions.TryGetValue(obj.Id, out long lastVersion) && lastVersion == obj.Version)
                {
                    continue;
                }

                // Check if we have enough space for the basic object data
                // Estimate: ID(5) + Version(5) + Type(5) + 3*Int(4) + PropCount(5) = 32 bytes
                if (offset + 32 > destination.Length)
                {
                    truncated = true;
                    break;
                }

                int startOffset = offset;
                offset += WriteVarInt(destination.Slice(offset), obj.Id);
                offset += WriteVarInt(destination.Slice(offset), obj.Version);
                offset += WriteVarInt(destination.Slice(offset), (long)(obj.ObjectType?.Id ?? -1));
                offset += WriteVarInt(destination.Slice(offset), obj.X);
                offset += WriteVarInt(destination.Slice(offset), obj.Y);
                offset += WriteVarInt(destination.Slice(offset), obj.Z);

                if (obj.ObjectType != null)
                {
                    var delta = obj.GetDeltaState();
                    offset += WriteVarInt(destination.Slice(offset), delta.Count);
                    if (delta.Changes != null)
                    {
                        for (int i = 0; i < delta.Count; i++)
                        {
                            var change = delta.Changes[i];
                            int propIdx = change.Index;
                            var val = change.Value;
                            int valueSize = val.GetWriteSize();
                            // 5 for property index varint max + value size
                            if (offset + 5 + valueSize > destination.Length)
                            {
                                // Roll back to start of object and mark as truncated
                                offset = startOffset;
                                truncated = true;
                                goto Done;
                            }

                            offset += WriteVarInt(destination.Slice(offset), propIdx);
                            offset += val.WriteTo(destination.Slice(offset));
                        }
                    }
                }
                else
                {
                    offset += WriteVarInt(destination.Slice(offset), 0);
                }

                if (lastVersions != null) lastVersions[obj.Id] = obj.Version;
            }

        Done:
            if (offset < destination.Length)
            {
                offset += WriteVarInt(destination.Slice(offset), 0); // End of stream marker
            }

            return offset;
        }

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


        private int WriteVarInt(Span<byte> span, long value)
        {
            ulong v = (ulong)value;
            int count = 0;
            while (v >= 0x80)
            {
                span[count++] = (byte)(v | 0x80);
                v >>= 7;
            }
            span[count++] = (byte)v;
            return count;
        }

        public long ReadVarInt(ReadOnlySpan<byte> span, out int bytesRead)
        {
            long result = 0;
            int shift = 0;
            bytesRead = 0;
            while (true)
            {
                byte b = span[bytesRead++];
                result |= (long)(b & 0x7f) << shift;
                if ((b & 0x80) == 0) return result;
                shift += 7;
            }
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
