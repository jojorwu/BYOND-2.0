using Shared.Enums;
using Shared.Interfaces;
using Shared.Utils;
using Shared.Models;

namespace Shared.Networking.FieldHandlers;

public class VariablesFieldHandler : INetworkFieldHandler
{
    public GameObjectFields FieldMask => GameObjectFields.Variables;
    public int Priority => 30;

    public void Write(ref BitWriter writer, IGameObject obj, GameObjectFields currentMask)
    {
        if (obj is GameObject g)
        {
            var counter = new ChangeCounter();
            g.VisitChanges(ref counter);
            writer.WriteVarInt(counter.Count);

            var serializer = new BitChangeSerializer { Writer = writer };
            g.VisitChanges(ref serializer);
            writer = serializer.Writer;
        }
    }

    public void Read(ref BitReader reader, GameObject obj, GameObjectFields currentMask)
    {
        int propertyCount = (int)reader.ReadVarInt();
        // Since we don't have unresolvedReferences here easily, this logic stays in serializer for now
        // OR we pass the unresolved list.
        // Let's keep variables and components in the serializer for now as they have complex dependencies.
    }

    public void Skip(ref BitReader reader, GameObjectFields currentMask)
    {
        int count = (int)reader.ReadVarInt();
        for (int i = 0; i < count; i++) { reader.ReadVarInt(); DreamValue.BitReadFrom(ref reader); }
    }

    public struct ChangeCounter : GameObject.IChangeVisitor
    {
        public int Count;
        public void Visit(int index, in DreamValue value) => Count++;
    }

    private ref struct BitChangeSerializer : GameObject.IChangeVisitor
    {
        public BitWriter Writer;
        public void Visit(int propIdx, in DreamValue val)
        {
            Writer.WriteVarInt(propIdx);
            val.BitWriteTo(ref Writer);
        }
    }
}
