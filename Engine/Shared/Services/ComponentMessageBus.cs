using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services;
    public class ComponentMessageBus : EngineService, IComponentMessageBus
    {
        private readonly IComponentManager _componentManager;
        private readonly ConcurrentDictionary<Type, int[]> _targetIdsCache = new();

        public ComponentMessageBus(IComponentManager componentManager)
        {
            _componentManager = componentManager;
        }

        public void SendMessage(IGameObject target, IComponentMessage message)
        {
            // Use the optimized GameObject method which utilizes local component cache
            if (target is GameObject gameObject)
            {
                gameObject.SendMessage(message);
            }
            else
            {
                var components = _componentManager.GetAllComponents(target);
                foreach (var component in components)
                {
                    if (component.Enabled)
                    {
                        component.OnMessage(message);
                    }
                }
            }
        }

        public void BroadcastMessage(IComponentMessage message)
        {
            if (_componentManager is ComponentManager cm)
            {
                var am = cm.ArchetypeManager;
                var targetTypes = message.TargetComponentTypes;
                if (targetTypes != null && targetTypes.Length > 0)
                {
                    var archetypes = am.GetArchetypesWithComponents(targetTypes);

                    var filterMask = new ComponentMask();
                    var targetIds = _targetIdsCache.GetOrAdd(message.GetType(), _ =>
                    {
                        var ids = new int[targetTypes.Length];
                        for (int i = 0; i < targetTypes.Length; i++)
                        {
                            ids[i] = ComponentIdRegistry.GetId(targetTypes[i]);
                        }
                        return ids;
                    });

                    foreach (var id in targetIds)
                    {
                        filterMask.Set(id);
                    }

                    foreach (var arch in archetypes)
                    {
                        // Rapidly skip archetypes that don't overlap with our targets
                        if (!arch.Signature.Mask.Overlaps(filterMask)) continue;

                        var signatureIds = arch.Signature.ComponentIds;
                        int entityCount = arch.EntityCount;

                        for (int i = 0; i < targetIds.Length; i++)
                        {
                            int targetId = targetIds[i];
                            var array = arch.GetComponentsInternal(targetId);
                            if (array == null) continue;

                            for (int j = 0; j < entityCount; j++)
                            {
                                var component = array.Get(j);
                                if (component != null && component.Enabled)
                                {
                                    component.OnMessage(message);
                                }
                            }
                        }
                    }
                }
                else
                {
                    cm.ArchetypeManager.ForEachEntity(entity =>
                    {
                        entity.SendMessage(message);
                    });
                }
            }
        }
    }
