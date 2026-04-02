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

    public int SnapshotStateSize => (3 * sizeof(long)) + sizeof(float);

    public void SaveState(Span<byte> destination, IGameObject obj)
    {
        long x = obj.Position.X;
        long y = obj.Position.Y;
        long z = obj.Position.Z;
        float rot = obj.Rotation;
        System.Runtime.InteropServices.MemoryMarshal.Write(destination.Slice(0, 8), in x);
        System.Runtime.InteropServices.MemoryMarshal.Write(destination.Slice(8, 8), in y);
        System.Runtime.InteropServices.MemoryMarshal.Write(destination.Slice(16, 8), in z);
        System.Runtime.InteropServices.MemoryMarshal.Write(destination.Slice(24, 4), in rot);
    }

    public void Interpolate(IGameObject obj, ReadOnlySpan<byte> from, ReadOnlySpan<byte> to, double t)
    {
        long fx = System.Runtime.InteropServices.MemoryMarshal.Read<long>(from.Slice(0, 8));
        long fy = System.Runtime.InteropServices.MemoryMarshal.Read<long>(from.Slice(8, 8));
        long fz = System.Runtime.InteropServices.MemoryMarshal.Read<long>(from.Slice(16, 8));
        float fr = System.Runtime.InteropServices.MemoryMarshal.Read<float>(from.Slice(24, 4));

        long tx = System.Runtime.InteropServices.MemoryMarshal.Read<long>(to.Slice(0, 8));
        long ty = System.Runtime.InteropServices.MemoryMarshal.Read<long>(to.Slice(8, 8));
        long tz = System.Runtime.InteropServices.MemoryMarshal.Read<long>(to.Slice(16, 8));
        float tr = System.Runtime.InteropServices.MemoryMarshal.Read<float>(to.Slice(24, 4));

        if (obj is GameObject g)
        {
            g.RenderState.X = fx + (tx - fx) * t;
            g.RenderState.Y = fy + (ty - fy) * t;
            g.RenderState.Z = fz + (tz - fz) * t;

            g.RenderState.PixelX = (g.RenderState.X - tx) * 32;
            g.RenderState.PixelY = (g.RenderState.Y - ty) * 32;

            float diff = tr - fr;
            while (diff < -180) diff += 360;
            while (diff > 180) diff -= 360;
            g.RenderState.Rotation = fr + diff * (float)t;
        }
    }
}
