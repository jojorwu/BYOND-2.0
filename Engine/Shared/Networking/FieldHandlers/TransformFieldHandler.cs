using Shared.Enums;
using Shared.Interfaces;
using Shared.Utils;
using Shared.Models;

namespace Shared.Networking.FieldHandlers;

public class TransformFieldHandler : INetworkFieldHandler
{
    public GameObjectFields FieldMask => GameObjectFields.Position | GameObjectFields.Rotation;
    public int Priority => 10;

    public void Write(ref BitWriter writer, IGameObject obj, GameObjectFields currentMask)
    {
        if ((currentMask & GameObjectFields.PositionX) != 0) writer.WriteZigZag(obj.X);
        if ((currentMask & GameObjectFields.PositionY) != 0) writer.WriteZigZag(obj.Y);
        if ((currentMask & GameObjectFields.PositionZ) != 0) writer.WriteZigZag(obj.Z);
        if ((currentMask & GameObjectFields.Rotation) != 0) writer.WriteDouble(obj.Rotation);
    }

    public void Read(ref BitReader reader, GameObject obj, GameObjectFields currentMask)
    {
        if ((currentMask & GameObjectFields.PositionX) != 0) obj.X = reader.ReadZigZag();
        if ((currentMask & GameObjectFields.PositionY) != 0) obj.Y = reader.ReadZigZag();
        if ((currentMask & GameObjectFields.PositionZ) != 0) obj.Z = reader.ReadZigZag();
        if ((currentMask & GameObjectFields.Rotation) != 0) obj.Rotation = (float)reader.ReadDouble();
    }

    public void Skip(ref BitReader reader, GameObjectFields currentMask)
    {
        if ((currentMask & GameObjectFields.PositionX) != 0) reader.ReadZigZag();
        if ((currentMask & GameObjectFields.PositionY) != 0) reader.ReadZigZag();
        if ((currentMask & GameObjectFields.PositionZ) != 0) reader.ReadZigZag();
        if ((currentMask & GameObjectFields.Rotation) != 0) reader.ReadDouble();
    }
}
