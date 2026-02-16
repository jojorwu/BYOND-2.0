using System;
using System.IO;
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
            using (var ms = new MemoryStream())
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
                            writer.Write(g.X);
                            writer.Write(g.Y);
                            writer.Write(g.Z);

                            // Optimized property serialization
                            var props = g.Properties;
                            WriteVarInt(writer, props.Count);
                            foreach (var kvp in props)
                            {
                                writer.Write(kvp.Key);
                                kvp.Value.WriteTo(writer);
                            }

                            if (lastVersions != null) lastVersions[g.Id] = g.Version;
                        }
                    }
                    WriteVarInt(writer, 0); // End of stream marker
                }
                return ms.ToArray();
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

        public List<GameObject> Deserialize(byte[] data, IObjectTypeManager typeManager)
        {
            var objects = new List<GameObject>();
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new BinaryReader(ms))
                {
                    while (ms.Position < ms.Length)
                    {
                        int id = ReadVarInt(reader);
                        if (id == 0) break;

                        int version = ReadVarInt(reader);
                        int x = reader.ReadInt32();
                        int y = reader.ReadInt32();
                        int z = reader.ReadInt32();

                        var gameObject = new GameObject(); // We might need a factory here
                        gameObject.Id = id;
                        gameObject.SetPosition(x, y, z);

                        int propCount = ReadVarInt(reader);
                        for (int i = 0; i < propCount; i++)
                        {
                            string key = reader.ReadString();
                            if (_interner != null) key = _interner.Intern(key);
                            var val = DreamValue.ReadFrom(reader);
                            gameObject.SetVariable(key, val);
                        }
                        objects.Add(gameObject);
                    }
                }
            }
            return objects;
        }
    }
}
