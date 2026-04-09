using System;
using System.Collections.Generic;
using Shared.Utils;

using Shared.Buffers;
namespace Shared.Interfaces;

/// <summary>
/// Defines a contract for serializing game state snapshots into binary format.
/// </summary>
public interface ISnapshotSerializer
{
    /// <summary>
    /// Serializes a collection of game objects into a bit-packed delta format.
    /// </summary>
    void SerializeBitPackedDelta(ref BitWriter writer, IEnumerable<IGameObject> objects, IDictionary<long, long>? lastVersions);

    /// <summary>
    /// Deserializes a bit-packed delta into the game world.
    /// </summary>
    void DeserializeBitPacked(ref BitReader reader, IDictionary<long, GameObject> world, IObjectTypeManager typeManager, IObjectFactory factory);
}
