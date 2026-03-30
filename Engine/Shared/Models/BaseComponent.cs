using Shared.Enums;
using Shared.Interfaces;

namespace Shared.Models;

public abstract class BaseComponent : IComponent
{
    public IGameObject? Owner { get; set; }
    public bool Enabled { get; set; } = true;

    public virtual void Initialize() { }
    public virtual void Shutdown() { }
    public virtual void OnMessage(IComponentMessage message) { }

    public void SendMessage(IComponentMessage message)
    {
        Owner?.SendMessage(message);
    }

    public virtual void BeginUpdate() { }
    public virtual void CommitUpdate() { }

    public virtual void Reset()
    {
        Owner = null;
        Enabled = true;
    }
}
