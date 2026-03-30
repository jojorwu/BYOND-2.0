using System;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Utils;
using Shared.Models;

namespace Shared.Services;

public class BitPackedSnapshotSerializer : ISnapshotSerializer
{
    [Flags]
    private enum GameObjectFields : byte
    {
        None = 0,
        Position = 1 << 0,
        Visuals = 1 << 1,
        Variables = 1 << 2,
        Type = 1 << 3,
        NewObject = 1 << 4
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
                    writer.WriteString(g.Icon);
                    writer.WriteString(g.IconState);
                    writer.WriteString(g.Color);
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

    public void DeserializeBitPacked(ReadOnlySpan<byte> data, IDictionary<long, GameObject> world, IObjectTypeManager typeManager, IObjectFactory factory)
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
                string icon = reader.ReadString();
                string iconState = reader.ReadString();
                string color = reader.ReadString();

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
}
