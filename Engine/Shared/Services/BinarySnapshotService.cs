using System;
using System.IO;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using Shared.Interfaces;

namespace Shared.Services
{
    public class BinarySnapshotService
    {
        private readonly StringInterner? _interner;

        public BinarySnapshotService(StringInterner? interner = null)
        {
            _interner = interner;
        }

        /// <summary>
        /// Serializes a collection of game objects into a binary snapshot.
        /// Only objects with changed versions compared to <paramref name="lastVersions"/> are included.
        /// </summary>
        public byte[] Serialize(IEnumerable<IGameObject> objects, IDictionary<int, long>? lastVersions = null)
        {
            var rentedBuffer = ArrayPool<byte>.Shared.Rent(65536);
            try
            {
                int offset = SerializeTo(objects, rentedBuffer, lastVersions);
                return rentedBuffer.AsSpan(0, offset).ToArray();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }

        public int SerializeTo(IEnumerable<IGameObject> objects, byte[] buffer, IDictionary<int, long>? lastVersions = null, int startOffset = 0)
        {
            var rentedBuffer = buffer;
            int offset = startOffset;

            foreach (var obj in objects)
                {
                    if (obj is not GameObject g) continue;

                    if (lastVersions != null && lastVersions.TryGetValue(g.Id, out long lastVersion) && lastVersion == g.Version)
                    {
                        continue;
                    }

                    // Pre-calculate required space for basic fields
                    // Max VarInt size for 32-bit int is 5 bytes.
                    // Basic fields: ID(5), Version(5), Type(5), X(4), Y(4), Z(4) = 27 bytes.
                    EnsureBuffer(ref rentedBuffer, offset, 32);

                    offset += WriteVarInt(rentedBuffer.AsSpan(offset), g.Id);
                    offset += WriteVarInt(rentedBuffer.AsSpan(offset), (int)g.Version);
                    offset += WriteVarInt(rentedBuffer.AsSpan(offset), g.ObjectType?.Id ?? -1);

                    var span = rentedBuffer.AsSpan(offset);
                    BinaryPrimitives.WriteInt32LittleEndian(span, g.X);
                    BinaryPrimitives.WriteInt32LittleEndian(span.Slice(4), g.Y);
                    BinaryPrimitives.WriteInt32LittleEndian(span.Slice(8), g.Z);
                    offset += 12;

                    if (g.ObjectType != null)
                    {
                        var varNames = g.ObjectType.VariableNames;
                        EnsureBuffer(ref rentedBuffer, offset, 5);
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
                        EnsureBuffer(ref rentedBuffer, offset, 1);
                        offset += WriteVarInt(rentedBuffer.AsSpan(offset), 0);
                    }

                    if (lastVersions != null) lastVersions[g.Id] = g.Version;
                }

            EnsureBuffer(ref rentedBuffer, offset, 1);
            offset += WriteVarInt(rentedBuffer.AsSpan(offset), 0); // End of stream marker

            return offset;
        }

        private void EnsureBuffer(ref byte[] buffer, int offset, int required)
        {
            if (offset + required > buffer.Length)
            {
                int newSize = Math.Max(buffer.Length * 2, offset + required);
                var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
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

        /// <summary>
        /// Deserializes a binary snapshot and updates the game world.
        /// </summary>
        public void Deserialize(byte[] data, IDictionary<int, GameObject> world, IObjectTypeManager typeManager, IObjectFactory factory)
        {
            if (data == null || data.Length == 0) return;

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
}
