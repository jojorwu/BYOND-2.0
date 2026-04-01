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
        var counter = new ComponentCounter();
        obj.VisitComponents(ref counter);
        writer.WriteVarInt(counter.Count);

        var serializer = new ComponentSerializer { Writer = writer };
        obj.VisitComponents(ref serializer);
        writer = serializer.Writer;
    }

    private struct ComponentCounter : IComponentVisitor
    {
        public int Count;
        public void Visit(IComponent component) => Count++;
    }

    private ref struct ComponentSerializer : IComponentVisitor
    {
        public BitWriter Writer;
        public void Visit(IComponent comp)
        {
            Writer.WriteString(comp.GetType().Name);

            int sizeFieldOffset = Writer.BitsWritten;
            Writer.WriteInt(0, 16);

            int startBits = Writer.BitsWritten;
            comp.WriteState(ref Writer);
            int endBits = Writer.BitsWritten;

            int payloadBits = endBits - startBits;
            Writer.PatchBits(sizeFieldOffset, (ulong)payloadBits, 16);
        }
    }

    public void Read(ref BitReader reader, GameObject obj, GameObjectFields currentMask)
    {
        int compCount = (int)reader.ReadVarInt();
        for (int i = 0; i < compCount; i++)
        {
            string compTypeName = reader.ReadString();
            int payloadBits = reader.ReadInt(16);

            var finder = new ComponentFinder { TypeName = compTypeName };
            obj.VisitComponents(ref finder);

            if (finder.Found != null)
            {
                int startBits = reader.BitsRead;
                finder.Found.ReadState(ref reader);
                int actualRead = reader.BitsRead - startBits;
                if (actualRead != payloadBits) reader.SkipBits(payloadBits - actualRead);
            }
            else
            {
                reader.SkipBits(payloadBits);
            }
        }
    }

    private struct ComponentFinder : IComponentVisitor
    {
        public string TypeName;
        public IComponent? Found;
        public void Visit(IComponent component)
        {
            if (Found == null && component.GetType().Name == TypeName) Found = component;
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
