using System;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Utils;
using Shared.Models;
using Shared.Enums;
using Shared.Networking.Messages;
using System.Linq;
using Shared.Attributes;

namespace Shared.Services;

[EngineService(typeof(ISnapshotSerializer))]
public class BitPackedSnapshotSerializer : EngineService, ISnapshotSerializer
{
    private readonly List<INetworkFieldHandler> _fieldHandlers;

    public BitPackedSnapshotSerializer(IEnumerable<INetworkFieldHandler> fieldHandlers)
    {
        _fieldHandlers = fieldHandlers.OrderBy(h => h.Priority).ToList();
    }

    public void SerializeBitPackedDelta(ref BitWriter writer, IEnumerable<IGameObject> objects, IDictionary<long, long>? lastVersions)
    {
        var handlers = _fieldHandlers;
        int handlerCount = handlers.Count;


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

            var compCounter = new Shared.Networking.FieldHandlers.ComponentsFieldHandler.ComponentCounter { IsNew = isNew };
            obj.VisitComponents(ref compCounter);
            if (compCounter.DirtyCount > 0) mask |= GameObjectFields.Components;

            if (isNew) mask |= GameObjectFields.NewObject | GameObjectFields.Type | GameObjectFields.Position | GameObjectFields.Visuals;

            int changeCount = 0;
            var counter = new Shared.Networking.FieldHandlers.VariablesFieldHandler.ChangeCounter();
            obj.Variables.VisitModified(ref counter);
            changeCount = counter.Count;
            if (changeCount > 0) mask |= GameObjectFields.Variables;

            writer.WriteBits((ulong)mask, 32);

            // Fast-path: If the object is in an Archetype, use bulk SoA write
            if (obj.Archetype is Archetype arch && obj.ArchetypeIndex != -1)
            {
                // We don't have the chunk here, but we can call a simplified bulk writer on the Archetype
                // For now, use the SoA-optimized field handler methods via dynamic chunk resolution
                // OR just call individual handlers with the object (they use SoA accessors internally now).

                // Optimization: Use specialized SoA write for core fields
                for (int i = 0; i < handlerCount; i++)
                {
                    var handler = handlers[i];
                    if ((mask & handler.FieldMask) != 0)
                    {
                        // Some handlers might benefit from knowing they are in an archetype
                        // but most just use the GameObject properties which we already optimized.
                        handler.Write(ref writer, obj, mask);
                    }
                }
            }
            else
            {
                // Slow-path for non-archetype objects
                for (int i = 0; i < handlerCount; i++)
                {
                    var handler = handlers[i];
                    if ((mask & handler.FieldMask) != 0)
                    {
                        handler.Write(ref writer, obj, mask);
                    }
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
            GameObjectFields mask = (GameObjectFields)reader.ReadBits(32);

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
                var handlers = _fieldHandlers;
                int handlerCount = handlers.Count;
                for (int i = 0; i < handlerCount; i++)
                {
                    var handler = handlers[i];
                    if ((mask & handler.FieldMask) != 0)
                    {
                        handler.Read(ref reader, gameObject, mask, unresolvedReferences);
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
