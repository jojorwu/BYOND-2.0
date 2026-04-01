using Shared.Enums;
using Shared.Interfaces;
using Shared.Utils;
using Shared.Models;

namespace Shared.Networking.FieldHandlers;

public class ComponentsFieldHandler : INetworkFieldHandler
{
    public GameObjectFields FieldMask => GameObjectFields.Components;
    public int Priority => 40;

    public void Write(ref BitWriter writer, IGameObject obj, GameObjectFields currentMask)
    {
        if (obj is GameObject g)
        {
            var components = g.GetComponents().ToList();
            writer.WriteVarInt(components.Count);
            foreach (var comp in components)
            {
                writer.WriteString(comp.GetType().Name);

                int sizeFieldOffset = writer.BitsWritten;
                writer.WriteInt(0, 16);

                int startBits = writer.BitsWritten;
                comp.WriteState(ref writer);
                int endBits = writer.BitsWritten;

                int payloadBits = endBits - startBits;
                writer.PatchBits(sizeFieldOffset, (ulong)payloadBits, 16);
            }
        }
        else
        {
            writer.WriteVarInt(0);
        }
    }

    public void Read(ref BitReader reader, GameObject obj, GameObjectFields currentMask)
    {
        int compCount = (int)reader.ReadVarInt();
        for (int i = 0; i < compCount; i++)
        {
            string compTypeName = reader.ReadString();
            int payloadBits = reader.ReadInt(16);

            var components = obj.GetComponents().ToList();
            var component = components.FirstOrDefault(c => c.GetType().Name == compTypeName);
            if (component != null)
            {
                int startBits = reader.BitsRead;
                component.ReadState(ref reader);
                int actualRead = reader.BitsRead - startBits;
                if (actualRead != payloadBits) reader.SkipBits(payloadBits - actualRead);
            }
            else
            {
                reader.SkipBits(payloadBits);
            }
        }
    }

    public void Skip(ref BitReader reader, GameObjectFields currentMask)
    {
        int compCount = (int)reader.ReadVarInt();
        for (int i = 0; i < compCount; i++)
        {
            reader.ReadString();
            int payloadBits = reader.ReadInt(16);
            reader.SkipBits(payloadBits);
        }
    }
}
