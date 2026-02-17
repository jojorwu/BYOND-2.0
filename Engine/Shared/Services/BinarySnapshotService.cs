using System;
using System.IO;
using System.Buffers;
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

        public byte[] Serialize(IEnumerable<IGameObject> objects, IDictionary<int, long>? lastVersions = null)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(65536);
            try
            {
                using (var ms = new MemoryStream(buffer))
                {
                    using (var writer = new BinaryWriter(ms))
                    {
                        foreach (var obj in objects)
                        {
                            if (obj is GameObject g)
                            {
                                if (lastVersions != null && lastVersions.TryGetValue(g.Id, out long lastVersion) && lastVersion == g.Version)
                                {
                                    continue;
                                }

                                WriteVarInt(writer, g.Id);
                                WriteVarInt(writer, (int)g.Version);
                                WriteVarInt(writer, g.ObjectType?.Id ?? -1);
                                writer.Write(g.X);
                                writer.Write(g.Y);
                                writer.Write(g.Z);

                                // High-efficiency indexed property serialization
                                if (g.ObjectType != null)
                                {
                                    var varNames = g.ObjectType.VariableNames;
                                    WriteVarInt(writer, varNames.Count);
                                    for (int i = 0; i < varNames.Count; i++)
                                    {
                                        // We only send the index, client resolves it via ObjectType
                                        WriteVarInt(writer, i);
                                        g.GetVariableDirect(i).WriteTo(writer);
                                    }
                                }
                                else
                                {
                                    WriteVarInt(writer, 0);
                                }

                                if (lastVersions != null) lastVersions[g.Id] = g.Version;
                            }
                        }
                        WriteVarInt(writer, 0); // End of stream marker

                        byte[] result = new byte[ms.Position];
                        Buffer.BlockCopy(buffer, 0, result, 0, (int)ms.Position);
                        return result;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private void WriteVarInt(BinaryWriter writer, int value)
        {
            uint v = (uint)value;
            while (v >= 0x80)
            {
                writer.Write((byte)(v | 0x80));
                v >>= 7;
            }
            writer.Write((byte)v);
        }

        public int ReadVarInt(BinaryReader reader)
        {
            int result = 0;
            int shift = 0;
            while (true)
            {
                byte b = reader.ReadByte();
                result |= (b & 0x7f) << shift;
                if ((b & 0x80) == 0) return result;
                shift += 7;
            }
        }

        public void Deserialize(byte[] data, IDictionary<int, GameObject> world, IObjectTypeManager typeManager, IObjectFactory factory)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new BinaryReader(ms))
                {
                    while (ms.Position < ms.Length)
                    {
                        int id = ReadVarInt(reader);
                        if (id == 0) break;

                        int version = ReadVarInt(reader);
                        int typeId = ReadVarInt(reader);
                        int x = reader.ReadInt32();
                        int y = reader.ReadInt32();
                        int z = reader.ReadInt32();

                        if (!world.TryGetValue(id, out var gameObject))
                        {
                            var type = typeManager.GetObjectType(typeId);
                            if (type == null) continue;

                            gameObject = factory.Create(type, x, y, z);
                            gameObject.Id = id;
                            world[id] = gameObject;
                        }
                        else
                        {
                            gameObject.SetPosition(x, y, z);
                        }

                        int propCount = ReadVarInt(reader);
                        for (int i = 0; i < propCount; i++)
                        {
                            int propIdx = ReadVarInt(reader);
                            var val = DreamValue.ReadFrom(reader);

                            // High-efficiency variable setting using indices
                            gameObject.SetVariableDirect(propIdx, val);
                        }
                    }
                }
            }
        }
    }
}
