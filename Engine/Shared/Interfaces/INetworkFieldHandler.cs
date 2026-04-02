using Shared.Enums;
using Shared.Utils;
using Shared.Models;

namespace Shared.Interfaces;

public interface INetworkFieldHandler
{
    GameObjectFields FieldMask { get; }
    int Priority { get; }

    void Write(ref BitWriter writer, IGameObject obj, GameObjectFields currentMask);
    void Read(ref BitReader reader, GameObject obj, GameObjectFields currentMask);
    void Skip(ref BitReader reader, GameObjectFields currentMask);

    int SnapshotStateSize { get; }
    void SaveState(Span<byte> destination, IGameObject obj);
    void Interpolate(IGameObject obj, ReadOnlySpan<byte> from, ReadOnlySpan<byte> to, double t);
}
