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

        /// <summary>
        /// Initializes a new instance of the <see cref="Turf"/> class.
        /// </summary>
        /// <param name="id">The identifier for the turf type.</param>
        public Turf(int id) : base(null!) // TODO: Pass proper ObjectType for turf
        {
            Id = id;
        }

        public Turf(ObjectType objectType, int x, int y, int z) : base(objectType, x, y, z)
        {
        }
    }
}
