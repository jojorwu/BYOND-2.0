namespace Core
{
    /// <summary>
    /// Represents the game map as a 3D grid of turfs.
    /// </summary>
    public class Map
    {
        private readonly Turf[,,] turfs;

        /// <summary>
        /// Gets the width of the map.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the height of the map.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the depth of the map.
        /// </summary>
        public int Depth { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Map"/> class.
        /// </summary>
        /// <param name="width">The width of the map.</param>
        /// <param name="height">The height of the map.</param>
        /// <param name="depth">The depth of the map.</param>
        public Map(int width, int height, int depth)
        {
            Width = width;
            Height = height;
            Depth = depth;
            turfs = new Turf[width, height, depth];
        }

        /// <summary>
        /// Gets the turf at the specified coordinates.
        /// </summary>
        /// <param name="x">The X-coordinate.</param>
        /// <param name="y">The Y-coordinate.</param>
        /// <param name="z">The Z-coordinate.</param>
        /// <returns>The turf at the specified coordinates, or null if the coordinates are out of bounds.</returns>
        public Turf? GetTurf(int x, int y, int z)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height || z < 0 || z >= Depth)
            {
                return null;
            }
            return turfs[x, y, z];
        }

        /// <summary>
        /// Sets the turf at the specified coordinates.
        /// </summary>
        /// <param name="x">The X-coordinate.</param>
        /// <param name="y">The Y-coordinate.</param>
        /// <param name="z">The Z-coordinate.</param>
        /// <param name="turf">The turf to set.</param>
        public void SetTurf(int x, int y, int z, Turf turf)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height && z >= 0 && z < Depth)
            {
                turfs[x, y, z] = turf;
            }
        }
    }
}
