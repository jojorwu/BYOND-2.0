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

        public byte[] Serialize(IEnumerable<IGameObject> objects, IDictionary<int, long>? lastVersions = null)
        {
            var rentedBuffer = ArrayPool<byte>.Shared.Rent(65536);
            int offset = 0;

            try
            {
                foreach (var obj in objects)
                {
                    if (obj is GameObject g)
                    {
                        if (lastVersions != null && lastVersions.TryGetValue(g.Id, out long lastVersion) && lastVersion == g.Version)
                        {
                            continue;
                        }

                        // Ensure buffer space (conservative estimate: ID(5) + Version(5) + Type(5) + 3*Int(4) + PropCount(5) + Props...)
                        EnsureBuffer(ref rentedBuffer, offset, 1024);

                        offset += WriteVarInt(rentedBuffer.AsSpan(offset), g.Id);
                        offset += WriteVarInt(rentedBuffer.AsSpan(offset), (int)g.Version);
                        offset += WriteVarInt(rentedBuffer.AsSpan(offset), g.ObjectType?.Id ?? -1);
                        BinaryPrimitives.WriteInt32LittleEndian(rentedBuffer.AsSpan(offset), g.X);
                        offset += 4;
                        BinaryPrimitives.WriteInt32LittleEndian(rentedBuffer.AsSpan(offset), g.Y);
                        offset += 4;
                        BinaryPrimitives.WriteInt32LittleEndian(rentedBuffer.AsSpan(offset), g.Z);
                        offset += 4;

                        if (g.ObjectType != null)
                        {
                            var varNames = g.ObjectType.VariableNames;
                            offset += WriteVarInt(rentedBuffer.AsSpan(offset), varNames.Count);
                            for (int i = 0; i < varNames.Count; i++)
                            {
                                var val = g.GetVariableDirect(i);
                                int needed = 5 + val.GetWriteSize(); // 5 for property index varint max + value size
                                EnsureBuffer(ref rentedBuffer, offset, needed);

                                offset += WriteVarInt(rentedBuffer.AsSpan(offset), i);
                                offset += val.WriteTo(rentedBuffer.AsSpan(offset));
                            }
                        }
                        else
                        {
                            offset += WriteVarInt(rentedBuffer.AsSpan(offset), 0);
                        }

                        if (lastVersions != null) lastVersions[g.Id] = g.Version;
                    }
                }

                EnsureBuffer(ref rentedBuffer, offset, 1);
                offset += WriteVarInt(rentedBuffer.AsSpan(offset), 0); // End of stream marker

                byte[] result = new byte[offset];
                Buffer.BlockCopy(rentedBuffer, 0, result, 0, offset);
                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }

        private void EnsureBuffer(ref byte[] buffer, int offset, int required)
        {
            if (offset + required > buffer.Length)
            {
                var newBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                Buffer.BlockCopy(buffer, 0, newBuffer, 0, offset);
                ArrayPool<byte>.Shared.Return(buffer);
                buffer = newBuffer;
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

        public void Deserialize(byte[] data, IDictionary<int, GameObject> world, IObjectTypeManager typeManager, IObjectFactory factory)
        {
            ReadOnlySpan<byte> span = data;
            int offset = 0;
            var unresolvedReferences = new List<(GameObject target, int propIdx, int refId)>();

            while (offset < span.Length)
            {
                int id = ReadVarInt(span.Slice(offset), out int idBytes);
                offset += idBytes;
                if (id == 0) break;

                GameObject.EnsureNextId(id);

                int version = ReadVarInt(span.Slice(offset), out int vBytes);
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
