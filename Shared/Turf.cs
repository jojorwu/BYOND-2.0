using System.Collections.Generic;

namespace Shared
{
    /// <summary>
    /// Represents a tile on the game map.
    /// </summary>
    public class Turf : GameObject, ITurf
    {
        /// <summary>
        /// Gets the list of game objects currently on this turf.
        /// </summary>
        public List<IGameObject> Contents { get; } = new List<IGameObject>();

        public Turf(ObjectType objectType, int x, int y, int z) : base(objectType, x, y, z)
        {
        }
    }
}
