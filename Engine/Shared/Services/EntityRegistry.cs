using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services;

public class EntityRegistry : EngineService, IEntityRegistry, IFreezable
{
    public IComponentManager ComponentManager { get; }
    public IObjectPool<GameObject> EntityPool { get; }

    public EntityRegistry(IObjectPool<GameObject> entityPool, IComponentManager componentManager)
    {
        EntityPool = entityPool;
        ComponentManager = componentManager;
    }

    public GameObject CreateEntity(ObjectType objectType, long x = 0, long y = 0, long z = 0)
    {
        var entity = EntityPool.Rent();
        entity.SetComponentManager(ComponentManager);
        entity.Initialize(objectType, x, y, z);
        return entity;
    }

    public void DestroyEntity(GameObject entity)
    {
        ResetEntity(entity);
        EntityPool.Return(entity);
    }

    public void ResetEntity(GameObject entity)
    {
        // Internal Reset logic moved here to centralize authority
        entity.Reset();

        // Ensure components are cleaned up via ComponentManager
        var components = ComponentManager.GetAllComponents(entity).ToList();
        foreach (var component in components)
        {
            ComponentManager.RemoveComponent(entity, component.GetType());
        }
    }

    public void Freeze()
    {
        // No-op for now, but allows participation in the freezing lifecycle
    }
}
