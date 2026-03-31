using System;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Utils;
using Shared.Models;
using Shared.Enums;

namespace Shared.Services;

public class BitPackedSnapshotSerializer : ISnapshotSerializer
{
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

            GameObjectFields mask = obj.GetChangeMask();
            if (isNew) mask |= GameObjectFields.NewObject | GameObjectFields.Type | GameObjectFields.Position | GameObjectFields.Visuals;

            var g = obj as GameObject;
            int changeCount = 0;
            if (g != null)
            {
                var counter = new ChangeCounter();
                g.VisitChanges(ref counter);
                changeCount = counter.Count;
                if (changeCount > 0) mask |= GameObjectFields.Variables;
            }

            writer.WriteBits((ulong)mask, 16);

            if ((mask & GameObjectFields.Type) != 0)
            {
                writer.WriteVarInt(obj.ObjectType?.Id ?? -1);
            }

            if ((mask & GameObjectFields.PositionX) != 0) writer.WriteZigZag(obj.X);
            if ((mask & GameObjectFields.PositionY) != 0) writer.WriteZigZag(obj.Y);
            if ((mask & GameObjectFields.PositionZ) != 0) writer.WriteZigZag(obj.Z);

            if ((mask & GameObjectFields.Dir) != 0) writer.WriteVarInt(obj.Dir);
            if ((mask & GameObjectFields.Alpha) != 0) writer.WriteDouble(obj.Alpha);
            if ((mask & GameObjectFields.Color) != 0) writer.WriteString(obj.Color);
            if ((mask & GameObjectFields.Layer) != 0) writer.WriteDouble(obj.Layer);
            if ((mask & GameObjectFields.Icon) != 0) writer.WriteString(obj.Icon);
            if ((mask & GameObjectFields.IconState) != 0) writer.WriteString(obj.IconState);
            if ((mask & GameObjectFields.PixelX) != 0) writer.WriteDouble(obj.PixelX);
            if ((mask & GameObjectFields.PixelY) != 0) writer.WriteDouble(obj.PixelY);
            if ((mask & GameObjectFields.Rotation) != 0) writer.WriteDouble(obj.Rotation);

            if ((mask & GameObjectFields.Variables) != 0 && g != null)
            {
                writer.WriteVarInt(changeCount);
                var serializer = new BitChangeSerializer { Writer = writer };
                g.VisitChanges(ref serializer);
                writer = serializer.Writer;
            }

            if (lastVersions != null) lastVersions[obj.Id] = obj.Version;
            obj.ClearChangeMask();
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
            GameObjectFields mask = (GameObjectFields)reader.ReadBits(16);

            GameObject? gameObject;
            world.TryGetValue(id, out gameObject);

            if ((mask & GameObjectFields.Type) != 0)
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

            if (gameObject != null && (gameObject.Version < version || (mask & GameObjectFields.NewObject) != 0))
            {
                if ((mask & GameObjectFields.PositionX) != 0) gameObject.X = reader.ReadZigZag();
                if ((mask & GameObjectFields.PositionY) != 0) gameObject.Y = reader.ReadZigZag();
                if ((mask & GameObjectFields.PositionZ) != 0) gameObject.Z = reader.ReadZigZag();

                if ((mask & GameObjectFields.Dir) != 0) gameObject.Dir = (int)reader.ReadVarInt();
                if ((mask & GameObjectFields.Alpha) != 0) gameObject.Alpha = reader.ReadDouble();
                if ((mask & GameObjectFields.Color) != 0) gameObject.Color = reader.ReadString();
                if ((mask & GameObjectFields.Layer) != 0) gameObject.Layer = reader.ReadDouble();
                if ((mask & GameObjectFields.Icon) != 0) gameObject.Icon = reader.ReadString();
                if ((mask & GameObjectFields.IconState) != 0) gameObject.IconState = reader.ReadString();
                if ((mask & GameObjectFields.PixelX) != 0) gameObject.PixelX = reader.ReadDouble();
                if ((mask & GameObjectFields.PixelY) != 0) gameObject.PixelY = reader.ReadDouble();
                if ((mask & GameObjectFields.Rotation) != 0) gameObject.Rotation = (float)reader.ReadDouble();

                if ((mask & GameObjectFields.Variables) != 0)
                {
                    int propertyCount = (int)reader.ReadVarInt();
                    for (int i = 0; i < propertyCount; i++)
                    {
                        int propIdx = (int)reader.ReadVarInt();
                        var val = DreamValue.BitReadFrom(ref reader);

                        if (val.IsObjectIdReference) unresolvedReferences.Add((gameObject, propIdx, val.ObjectId));
                        else gameObject.SetVariableDirect(propIdx, val);
                    }
                }
                gameObject.Version = version;
            }
            else
            {
                // Skip logic
                if ((mask & GameObjectFields.PositionX) != 0) reader.ReadZigZag();
                if ((mask & GameObjectFields.PositionY) != 0) reader.ReadZigZag();
                if ((mask & GameObjectFields.PositionZ) != 0) reader.ReadZigZag();
                if ((mask & GameObjectFields.Dir) != 0) reader.ReadVarInt();
                if ((mask & GameObjectFields.Alpha) != 0) reader.ReadDouble();
                if ((mask & GameObjectFields.Color) != 0) reader.ReadString();
                if ((mask & GameObjectFields.Layer) != 0) reader.ReadDouble();
                if ((mask & GameObjectFields.Icon) != 0) reader.ReadString();
                if ((mask & GameObjectFields.IconState) != 0) reader.ReadString();
                if ((mask & GameObjectFields.PixelX) != 0) reader.ReadDouble();
                if ((mask & GameObjectFields.PixelY) != 0) reader.ReadDouble();
                if ((mask & GameObjectFields.Rotation) != 0) reader.ReadDouble();
                if ((mask & GameObjectFields.Variables) != 0)
                {
                    int count = (int)reader.ReadVarInt();
                    for (int i = 0; i < count; i++) { reader.ReadVarInt(); DreamValue.BitReadFrom(ref reader); }
                }
            }
        }

        foreach (var (target, propIdx, refId) in unresolvedReferences)
        {
            if (world.TryGetValue(refId, out var refObj)) target.SetVariableDirect(propIdx, new DreamValue(refObj));
            else target.SetVariableDirect(propIdx, DreamValue.Null);
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
