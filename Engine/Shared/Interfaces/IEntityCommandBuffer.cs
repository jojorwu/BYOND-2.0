using System;
using Shared.Interfaces;

namespace Shared.Interfaces
{
    /// <summary>
    /// Records structural changes to game objects and components to be applied at a safe synchronization point.
    /// </summary>
    public interface IEntityCommandBuffer
    {
        void CreateObject(ObjectType objectType, int x = 0, int y = 0, int z = 0);
        void DestroyObject(IGameObject obj);
        void AddComponent<T>(IGameObject obj, T component) where T : class, IComponent;
        void RemoveComponent<T>(IGameObject obj) where T : class, IComponent;

        /// <summary>
        /// Plays back all recorded commands.
        /// </summary>
        void Playback();

        /// <summary>
        /// Clears all recorded commands without playing them back.
        /// </summary>
        void Clear();
    }
}
