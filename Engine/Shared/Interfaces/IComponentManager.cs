using System.Collections.Generic;

namespace Shared.Interfaces
{
    public interface IComponentManager
    {
        void AddComponent<T>(IGameObject owner, T component) where T : class, IComponent;
        void RemoveComponent<T>(IGameObject owner) where T : class, IComponent;
        T? GetComponent<T>(IGameObject owner) where T : class, IComponent;
        IEnumerable<T> GetComponents<T>() where T : class, IComponent;
        IEnumerable<IComponent> GetAllComponents(IGameObject owner);
    }
}
