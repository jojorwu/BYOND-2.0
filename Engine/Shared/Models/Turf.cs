using System.Collections.Generic;

namespace Shared;
    /// <summary>
    /// Represents a tile on the game map.
    /// </summary>
    public class Turf : GameObject, ITurf
    {
        public Turf(ObjectType objectType, long x, long y, long z) : base(objectType, x, y, z)
        {
            Id = (x << 32) | (uint)y; // Simple unique ID for turfs based on coords
        }
    }
