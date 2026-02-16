using Shared.Interfaces;

namespace Shared.Models
{
    public abstract class BaseComponent : IComponent
    {
        public IGameObject? Owner { get; set; }
        public bool Enabled { get; set; } = true;

        public virtual void Initialize() { }
        public virtual void Shutdown() { }

        public void SendMessage(IComponentMessage message)
        {
            Owner?.SendMessage(message);
        }

        public virtual void OnMessage(IComponentMessage message) { }
    }
}
