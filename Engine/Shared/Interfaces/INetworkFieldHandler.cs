using Shared.Enums;
using Shared.Utils;
using Shared.Models;

using Shared.Buffers;
namespace Shared.Interfaces;

public interface INetworkFieldHandler
{
    GameObjectFields FieldMask { get; }
    int Priority { get; }

    void Write(ref BitWriter writer, IGameObject obj, GameObjectFields currentMask);

    /// <summary>
    /// Bulk write version using SoA data.
    /// </summary>
    void Write<T>(ref BitWriter writer, ArchetypeChunk<T> chunk, int indexInChunk, GameObjectFields currentMask) where T : class, IComponent => Write(ref writer, chunk.Entities[chunk.Offset + indexInChunk], currentMask);

    void Read(ref BitReader reader, GameObject obj, GameObjectFields currentMask, List<(GameObject target, int propIdx, long refId)> unresolved);
    void Skip(ref BitReader reader, GameObjectFields currentMask);

    int SnapshotStateSize { get; }
    void SaveState(Span<byte> destination, IGameObject obj);
    void Interpolate(IGameObject obj, ReadOnlySpan<byte> from, ReadOnlySpan<byte> to, double t);
}
