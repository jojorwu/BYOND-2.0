using System.Collections.Generic;

namespace Shared;
    /// <summary>
    /// Represents a tile on the game map.
    /// </summary>
    public class Turf : GameObject, ITurf
    {
        public Turf(ObjectType objectType, long x, long y, long z) : base(objectType, x, y, z)
        {
            // Robust 64-bit ID generation using a bit mixer to prevent collisions across Z-levels and large coordinates.
            // Based on MurmurHash3's 64-bit finalizer for uniform distribution.
            ulong h = (ulong)x ^ ((ulong)y << 21) ^ ((ulong)z << 42);
            h ^= h >> 33;
            h *= 0xff51afd7ed558ccdUL;
            h ^= h >> 33;
            h *= 0xc4ceb9fe1a85ec53UL;
            h ^= h >> 33;

            Id = (long)(h & 0x7FFFFFFFFFFFFFFF); // Ensure ID is positive for engine consistency
        }
    }
