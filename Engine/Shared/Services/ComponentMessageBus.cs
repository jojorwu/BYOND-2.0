using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;

namespace Shared.Services
{
    public interface IComponentMessageBus
    {
        void SendMessage(IGameObject owner, IComponentMessage message);
    }

    public class ComponentMessageBus : IComponentMessageBus
    {
        private readonly IComponentManager _componentManager;

        public ComponentMessageBus(IComponentManager componentManager)
        {
            _componentManager = componentManager;
        }

        public void SendMessage(IGameObject owner, IComponentMessage message)
        {
            var components = _componentManager.GetAllComponents(owner);
            foreach (var component in components)
            {
                if (component.Enabled)
                {
                    component.OnMessage(message);
                }
            }
        }
    }
}
