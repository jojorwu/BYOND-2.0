using System.Collections.Generic;

namespace Core
{
    /// <summary>
    /// Represents a tile on the game map.
    /// </summary>
    public class Turf : ITurf
    {
        /// <summary>
        /// Gets or sets the identifier for the turf type.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets the list of game objects currently on this turf.
        /// </summary>
        public List<IGameObject> Contents { get; } = new List<IGameObject>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Turf"/> class.
        /// </summary>
        /// <param name="id">The identifier for the turf type.</param>
        public Turf(int id)
        {
            Id = id;
        }
    }
}
