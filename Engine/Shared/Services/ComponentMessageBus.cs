using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services;
    public class ComponentMessageBus : IComponentMessageBus
    {
        private readonly IComponentManager _componentManager;

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
                    var arrays = System.Buffers.ArrayPool<Shared.Models.Archetype.IComponentArray>.Shared.Rent(targetTypes.Length);

                    var filterMask = new ComponentMask();
                    foreach (var type in targetTypes)
                    {
                        filterMask.Set(ComponentIdRegistry.GetId(type));
                    }

                    try
                    {
                        foreach (var arch in archetypes)
                        {
                            // Rapidly skip archetypes that don't overlap with our targets
                            if (!arch.Signature.Mask.Overlaps(filterMask)) continue;

                            int arrayCount = 0;
                            for (int i = 0; i < targetTypes.Length; i++)
                            {
                                var array = arch.GetComponentsInternal(targetTypes[i]);
                                if (array != null) arrays[arrayCount++] = array;
                            }

                            if (arrayCount == 0) continue;

                            arch.ForEachEntity(entity =>
                            {
                                if (entity is GameObject g)
                                {
                                    int idx = g.ArchetypeIndex;
                                    for (int i = 0; i < arrayCount; i++)
                                    {
                                        var component = arrays[i].Get(idx);
                                        if (component != null && component.Enabled)
                                        {
                                            component.OnMessage(message);
                                        }
                                    }
                                }
                            });
                        }
                    }
                    finally
                    {
                        System.Buffers.ArrayPool<Shared.Models.Archetype.IComponentArray>.Shared.Return(arrays, clearArray: true);
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
