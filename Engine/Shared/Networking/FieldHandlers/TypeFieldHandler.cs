using Shared.Enums;
using Shared.Interfaces;
using Shared.Utils;
using Shared.Models;

namespace Shared.Networking.FieldHandlers;

public class TypeFieldHandler : INetworkFieldHandler
{
    public GameObjectFields FieldMask => GameObjectFields.Type;
    public int Priority => 5;

    public void Write(ref BitWriter writer, IGameObject obj, GameObjectFields currentMask)
    {
        writer.WriteVarInt(obj.ObjectType?.Id ?? -1);
    }

    public void Read(ref BitReader reader, GameObject obj, GameObjectFields currentMask)
    {
        // Type was already consumed for object creation/matching in DeserializeBitPacked
    }

    public void Skip(ref BitReader reader, GameObjectFields currentMask)
    {
        // Type was already consumed in DeserializeBitPacked before deciding to skip or read other fields
    }

    public int SnapshotStateSize => 0;
    public void SaveState(Span<byte> destination, IGameObject obj) { }
    public void Interpolate(IGameObject obj, ReadOnlySpan<byte> from, ReadOnlySpan<byte> to, double t) { }
}
