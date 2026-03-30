using System;
using System.Collections.Generic;

namespace Shared.Interfaces;

/// <summary>
/// Defines a contract for serializing game state snapshots into binary format.
/// </summary>
public interface ISnapshotSerializer
{
    /// <summary>
    /// Serializes a collection of game objects into a bit-packed delta format.
    /// </summary>
    int SerializeBitPackedDelta(Span<byte> destination, IEnumerable<IGameObject> objects, IDictionary<long, long>? lastVersions, out bool truncated);

    /// <summary>
    /// Deserializes a bit-packed delta into the game world.
    /// </summary>
    void DeserializeBitPacked(ReadOnlySpan<byte> data, IDictionary<long, GameObject> world, IObjectTypeManager typeManager, IObjectFactory factory);
}
