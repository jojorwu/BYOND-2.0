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

    public void Read(ref BitReader reader, GameObject obj, GameObjectFields currentMask, List<(GameObject target, int propIdx, long refId)> unresolved)
    {
        int propertyCount = (int)reader.ReadVarInt();
        for (int i = 0; i < propertyCount; i++)
        {
            int propIdx = (int)reader.ReadVarInt();
            var val = DreamValue.BitReadFrom(ref reader);
            if (val.IsObjectIdReference) unresolved.Add((obj, propIdx, val.ObjectId));
            else obj.SetVariableDirect(propIdx, val);
        }
    }

    public void Skip(ref BitReader reader, GameObjectFields currentMask)
    {
        int count = (int)reader.ReadVarInt();
        for (int i = 0; i < count; i++) { reader.ReadVarInt(); DreamValue.BitReadFrom(ref reader); }
    }

    public int SnapshotStateSize => 0;
    public void SaveState(Span<byte> destination, IGameObject obj) { }
    public void Interpolate(IGameObject obj, ReadOnlySpan<byte> from, ReadOnlySpan<byte> to, double t) { }

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
