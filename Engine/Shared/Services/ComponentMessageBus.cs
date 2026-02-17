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
        public void SendMessage(IGameObject owner, IComponentMessage message)
        {
            owner.SendMessage(message);
        }
    }
}
