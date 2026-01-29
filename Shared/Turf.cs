using System.Collections.Generic;

namespace Shared
{
    /// <summary>
    /// Represents a tile on the game map.
    /// </summary>
    public class Turf : GameObject, ITurf
    {
        public Turf(ObjectType objectType, int x, int y, int z) : base(objectType, x, y, z)
        {
        }
    }
}
