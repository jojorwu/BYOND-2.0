using System;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Utils;
using Shared.Models;
using Shared.Enums;
using Shared.Networking.Messages;

namespace Shared.Services;

public class BitPackedSnapshotSerializer : ISnapshotSerializer
{
    public void SerializeBitPackedDelta(ref BitWriter writer, IEnumerable<IGameObject> objects, IDictionary<long, long>? lastVersions)
    {
        foreach (var obj in objects)
        {
            bool isNew = lastVersions == null || !lastVersions.ContainsKey(obj.Id);
            if (!isNew && lastVersions != null && lastVersions.TryGetValue(obj.Id, out long lastVersion) && lastVersion == obj.Version)
            {
                continue;
            }

            writer.WriteVarInt(obj.Id);
            writer.WriteVarInt(obj.Version);

            GameObjectFields mask = obj.GetChangeMask();
            mask |= GameObjectFields.Components;
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

            if ((mask & GameObjectFields.Type) != 0) writer.WriteVarInt(obj.ObjectType?.Id ?? -1);

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

            if (g != null && (mask & GameObjectFields.Components) != 0)
            {
                var components = g.GetComponents().ToList();
                if (components.Count > 0)
                {
                    writer.WriteVarInt(components.Count);
                    foreach (var comp in components)
                    {
                        writer.WriteString(comp.GetType().Name);

                        int sizeFieldOffset = writer.BitsWritten;
                        writer.WriteInt(0, 16); // Placeholder for size in bits

                        int startBits = writer.BitsWritten;
                        comp.WriteState(ref writer);
                        int endBits = writer.BitsWritten;

                        int payloadBits = endBits - startBits;
                        writer.PatchBits(sizeFieldOffset, (ulong)payloadBits, 16);
                    }
                }
                else
                {
                    writer.WriteVarInt(0);
                }
            }

            if (lastVersions != null) lastVersions[obj.Id] = obj.Version;
            obj.ClearChangeMask();
        }

        writer.WriteVarInt(0); // End marker
    }

    public void DeserializeBitPacked(ref BitReader reader, IDictionary<long, GameObject> world, IObjectTypeManager typeManager, IObjectFactory factory)
    {
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

                if ((mask & GameObjectFields.Components) != 0)
                {
                    int compCount = (int)reader.ReadVarInt();
                    for (int i = 0; i < compCount; i++)
                    {
                        string compTypeName = reader.ReadString();
                        int payloadBits = reader.ReadInt(16);

                        // Find or create component
                        var components = gameObject.GetComponents().ToList();
                        var component = components.FirstOrDefault(c => c.GetType().Name == compTypeName);
                        if (component != null)
                        {
                            int startBits = reader.BitsRead;
                            component.ReadState(ref reader);
                            int actualRead = reader.BitsRead - startBits;
                            if (actualRead != payloadBits)
                            {
                                // Desync detected, skip to correct offset
                                reader.SkipBits(payloadBits - actualRead);
                            }
                        }
                        else
                        {
                            reader.SkipBits(payloadBits);
                        }
                    }
                }
                gameObject.Version = version;
            }
            else
            {
                SkipObject(ref reader, mask);
            }
        }

        foreach (var (target, propIdx, refId) in unresolvedReferences)
        {
            if (world.TryGetValue(refId, out var refObj)) target.SetVariableDirect(propIdx, new DreamValue(refObj));
            else target.SetVariableDirect(propIdx, DreamValue.Null);
        }
    }

    private void SkipObject(ref BitReader reader, GameObjectFields mask)
    {
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
        if ((mask & GameObjectFields.Type) != 0) reader.ReadVarInt();
        if ((mask & GameObjectFields.Variables) != 0)
        {
            int count = (int)reader.ReadVarInt();
            for (int i = 0; i < count; i++) { reader.ReadVarInt(); DreamValue.BitReadFrom(ref reader); }
        }

        if ((mask & GameObjectFields.Components) != 0)
        {
            int compCount = (int)reader.ReadVarInt();
            for (int i = 0; i < compCount; i++)
            {
                reader.ReadString(); // Type name
                int payloadBits = reader.ReadInt(16);
                reader.SkipBits(payloadBits);
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
