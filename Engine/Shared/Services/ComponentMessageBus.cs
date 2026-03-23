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
                    var targetIds = _targetIdsCache.GetOrAdd(message.GetType(), _ =>
                    {
                        var ids = new int[targetTypes.Length];
                        for (int i = 0; i < targetTypes.Length; i++)
                        {
                            ids[i] = ComponentIdRegistry.GetId(targetTypes[i]);
                        }
                        return ids;
                    });

                    foreach (var arch in am.GetArchetypesWithComponents(targetTypes))
                    {
                        int entityCount = arch.EntityCount;
                        if (entityCount == 0) continue;

                        foreach (var targetId in targetIds)
                        {
                            var array = arch.GetComponentsInternal(targetId);
                            if (array == null) continue;

                            // Optimized: dispatch to target component arrays within chunks
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
                    am.ForEachEntity(entity => entity.SendMessage(message));
                }
            }
        }
    }
