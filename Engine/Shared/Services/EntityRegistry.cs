using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services;

public class EntityRegistry : IEntityRegistry
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
        // Internal Reset logic handles component cleanup and state reset efficiently
        entity.Reset();
    }
}
