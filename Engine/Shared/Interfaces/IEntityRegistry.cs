using Shared.Models;

namespace Shared.Interfaces;

/// <summary>
/// Unifies entity (GameObject) management and component management.
/// Acts as a central authority for the lifecycle of game entities.
/// </summary>
public interface IEntityRegistry
{
    GameObject CreateEntity(ObjectType objectType, long x = 0, long y = 0, long z = 0);
    void DestroyEntity(GameObject entity);
    void ResetEntity(GameObject entity);

    IComponentManager ComponentManager { get; }
    IObjectPool<GameObject> EntityPool { get; }
    int EntityCount { get; }
}
