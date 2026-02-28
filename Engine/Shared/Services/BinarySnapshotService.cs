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
                // Estimate: ID(10) + Version(10) + Type(5) + 3*Int(4) + PropCount(5) = 45 bytes
                if (offset + 45 > destination.Length)
                {
                    truncated = true;
                    break;
                }

                int startOffset = offset;
                offset += WriteVarInt64(destination.Slice(offset), obj.Id);
                offset += WriteVarInt64(destination.Slice(offset), obj.Version);
                offset += WriteVarInt(destination.Slice(offset), obj.ObjectType?.Id ?? -1);
                BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset), obj.X);
                offset += 4;
                BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset), obj.Y);
                offset += 4;
                BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset), obj.Z);
                offset += 4;

                if (obj.ObjectType != null)
                {
                    var varNames = obj.ObjectType.VariableNames;
                    offset += WriteVarInt(destination.Slice(offset), varNames.Count);
                    for (int i = 0; i < varNames.Count; i++)
                    {
                        var val = obj.GetVariable(i);
                        int valueSize = val.GetWriteSize();
                        // 5 for property index varint max + value size
                        if (offset + 5 + valueSize > destination.Length)
                        {
                            // Roll back to start of object and mark as truncated
                            offset = startOffset;
                            truncated = true;
                            goto Done;
                        }

                        offset += WriteVarInt(destination.Slice(offset), i);
                        offset += val.WriteTo(destination.Slice(offset));
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


        private int WriteVarInt(Span<byte> span, int value)
        {
            uint v = (uint)value;
            int count = 0;
            while (v >= 0x80)
            {
                span[count++] = (byte)(v | 0x80);
                v >>= 7;
            }
            span[count++] = (byte)v;
            return count;
        }

        private int WriteVarInt64(Span<byte> span, long value)
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

        public int ReadVarInt(ReadOnlySpan<byte> span, out int bytesRead)
        {
            int result = 0;
            int shift = 0;
            bytesRead = 0;
            while (true)
            {
                byte b = span[bytesRead++];
                result |= (b & 0x7f) << shift;
                if ((b & 0x80) == 0) return result;
                shift += 7;
            }
        }

        public long ReadVarInt64(ReadOnlySpan<byte> span, out int bytesRead)
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
                long id = ReadVarInt64(span.Slice(offset), out int idBytes);
                offset += idBytes;
                if (id == 0) break;

                GameObject.EnsureNextId(id);

                long version = ReadVarInt64(span.Slice(offset), out int vBytes);
                offset += vBytes;
                int typeId = ReadVarInt(span.Slice(offset), out int tBytes);
                offset += tBytes;

                int x = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset));
                offset += 4;
                int y = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset));
                offset += 4;
                int z = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset));
                offset += 4;

                bool skip = false;
                if (world.TryGetValue(id, out var gameObject))
                {
                    if (gameObject.Version >= version) skip = true;
                }
                else
                {
                    var type = typeManager.GetObjectType(typeId);
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

                int propertyCount = ReadVarInt(span.Slice(offset), out int propCountBytes);
                offset += propCountBytes;
                for (int i = 0; i < propertyCount; i++)
                {
                    int propIdx = ReadVarInt(span.Slice(offset), out int propIdxBytes);
                    offset += propIdxBytes;
                    var val = DreamValue.ReadFrom(span.Slice(offset), out int valBytes);
                    offset += valBytes;

                    if (!skip && gameObject != null)
                    {
                        if (val.IsObjectIdReference)
                        {
                        unresolvedReferences.Add((gameObject, propIdx, (long)val.ObjectId));
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
