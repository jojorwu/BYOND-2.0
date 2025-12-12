using System.Collections.Generic;

namespace Shared
{
    /// <summary>
    /// Represents a tile on the game map.
    /// </summary>
    public class Turf : ITurf
    {
        private int _id;

        /// <summary>
        /// Gets or sets the identifier for the turf type.
        /// </summary>
        public int Id { get => _id; set { if(_id != value) { _id = value; IsDirty = true; } } }

        /// <summary>
        /// Gets the list of game objects currently on this turf.
        /// </summary>
        public List<IGameObject> Contents { get; } = new List<IGameObject>();

        /// <summary>
        /// Gets or sets a value indicating whether the turf has changed since the last snapshot.
        /// </summary>
        public bool IsDirty { get; set; } = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="Turf"/> class.
        /// </summary>
        /// <param name="id">The identifier for the turf type.</param>
        public Turf(int id)
        {
            _id = id;
        }
    }
}
