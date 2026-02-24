using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Shared.Interfaces;

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
                    foreach (var arch in archetypes)
                    {
                        // Get matching component arrays once per archetype
                        var arrays = new List<Shared.Models.Archetype.IComponentArray>();
                        foreach (var type in targetTypes)
                        {
                            var array = arch.GetComponentsInternal(type);
                            if (array != null) arrays.Add(array);
                        }

                        if (arrays.Count == 0) continue;

                        arch.ForEachEntity(entity =>
                        {
                            if (entity is GameObject g)
                            {
                                int idx = g.ArchetypeIndex;
                                foreach (var array in arrays)
                                {
                                    var component = array.Get(idx);
                                    if (component != null && component.Enabled)
                                    {
                                        component.OnMessage(message);
                                    }
                                }
                            }
                        });
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
