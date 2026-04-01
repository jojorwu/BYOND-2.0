using Shared.Enums;
using Shared.Interfaces;
using Shared.Utils;
using Shared.Models;

namespace Shared.Networking.FieldHandlers;

public class VisualFieldHandler : INetworkFieldHandler
{
    public GameObjectFields FieldMask => GameObjectFields.Visuals;
    public int Priority => 20;

    public void Write(ref BitWriter writer, IGameObject obj, GameObjectFields currentMask)
    {
        if ((currentMask & GameObjectFields.Dir) != 0) writer.WriteVarInt(obj.Dir);
        if ((currentMask & GameObjectFields.Alpha) != 0) writer.WriteDouble(obj.Alpha);
        if ((currentMask & GameObjectFields.Color) != 0) writer.WriteString(obj.Color);
        if ((currentMask & GameObjectFields.Layer) != 0) writer.WriteDouble(obj.Layer);
        if ((currentMask & GameObjectFields.Icon) != 0) writer.WriteString(obj.Icon);
        if ((currentMask & GameObjectFields.IconState) != 0) writer.WriteString(obj.IconState);
        if ((currentMask & GameObjectFields.PixelX) != 0) writer.WriteDouble(obj.PixelX);
        if ((currentMask & GameObjectFields.PixelY) != 0) writer.WriteDouble(obj.PixelY);
    }

    public void Read(ref BitReader reader, GameObject obj, GameObjectFields currentMask)
    {
        if ((currentMask & GameObjectFields.Dir) != 0) obj.Dir = (int)reader.ReadVarInt();
        if ((currentMask & GameObjectFields.Alpha) != 0) obj.Alpha = reader.ReadDouble();
        if ((currentMask & GameObjectFields.Color) != 0) obj.Color = reader.ReadString();
        if ((currentMask & GameObjectFields.Layer) != 0) obj.Layer = reader.ReadDouble();
        if ((currentMask & GameObjectFields.Icon) != 0) obj.Icon = reader.ReadString();
        if ((currentMask & GameObjectFields.IconState) != 0) obj.IconState = reader.ReadString();
        if ((currentMask & GameObjectFields.PixelX) != 0) obj.PixelX = reader.ReadDouble();
        if ((currentMask & GameObjectFields.PixelY) != 0) obj.PixelY = reader.ReadDouble();
    }

    public void Skip(ref BitReader reader, GameObjectFields currentMask)
    {
        if ((currentMask & GameObjectFields.Dir) != 0) reader.ReadVarInt();
        if ((currentMask & GameObjectFields.Alpha) != 0) reader.ReadDouble();
        if ((currentMask & GameObjectFields.Color) != 0) reader.ReadString();
        if ((currentMask & GameObjectFields.Layer) != 0) reader.ReadDouble();
        if ((currentMask & GameObjectFields.Icon) != 0) reader.ReadString();
        if ((currentMask & GameObjectFields.IconState) != 0) reader.ReadString();
        if ((currentMask & GameObjectFields.PixelX) != 0) reader.ReadDouble();
        if ((currentMask & GameObjectFields.PixelY) != 0) reader.ReadDouble();
    }
}
