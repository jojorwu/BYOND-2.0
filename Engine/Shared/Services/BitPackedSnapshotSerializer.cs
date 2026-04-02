using System;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Utils;
using Shared.Models;
using Shared.Enums;
using Shared.Networking.Messages;
using System.Linq;

namespace Shared.Services;

public class BitPackedSnapshotSerializer : ISnapshotSerializer
{
    private readonly List<INetworkFieldHandler> _fieldHandlers;

    public BitPackedSnapshotSerializer(IEnumerable<INetworkFieldHandler> fieldHandlers)
    {
        _fieldHandlers = fieldHandlers.OrderBy(h => h.Priority).ToList();
    }

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

            var compCounter = new Shared.Networking.FieldHandlers.ComponentsFieldHandler.ComponentCounter();
            obj.VisitComponents(ref compCounter);
            if (compCounter.DirtyCount > 0) mask |= GameObjectFields.Components;

            if (isNew) mask |= GameObjectFields.NewObject | GameObjectFields.Type | GameObjectFields.Position | GameObjectFields.Visuals | GameObjectFields.Components;

            int changeCount = 0;
            var counter = new Shared.Networking.FieldHandlers.VariablesFieldHandler.ChangeCounter();
            obj.Variables.VisitModified(ref counter);
            changeCount = counter.Count;
            if (changeCount > 0) mask |= GameObjectFields.Variables;

            writer.WriteBits((ulong)mask, 16);

            foreach (var handler in _fieldHandlers)
            {
                if ((mask & handler.FieldMask) != 0)
                {
                    handler.Write(ref writer, obj, mask);
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
                foreach (var handler in _fieldHandlers)
                {
                    if ((mask & handler.FieldMask) != 0)
                    {
                        // Handle Variables specially because of unresolvedReferences
                        if (handler is Shared.Networking.FieldHandlers.VariablesFieldHandler)
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
                        else
                        {
                            handler.Read(ref reader, gameObject, mask);
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
        foreach (var handler in _fieldHandlers)
        {
            if ((mask & handler.FieldMask) != 0)
            {
                handler.Skip(ref reader, mask);
            }
        }
    }
}
