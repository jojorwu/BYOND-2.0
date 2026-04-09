using Shared.Enums;
using Shared.Interfaces;
using Shared.Utils;
using Shared.Models;
using System.Linq;

using Shared.Buffers;
namespace Shared.Networking.FieldHandlers;

public class ComponentsFieldHandler : INetworkFieldHandler
{
    public GameObjectFields FieldMask => GameObjectFields.Components;
    public int Priority => 40;

    public void Write(ref BitWriter writer, IGameObject obj, GameObjectFields currentMask)
    {
        var counter = new ComponentCounter { IsNew = (currentMask & Enums.GameObjectFields.NewObject) != 0 };
        obj.VisitComponents(ref counter);
        writer.WriteVarInt(counter.DirtyCount);

        var serializer = new ComponentSerializer { Writer = writer, IsNew = counter.IsNew };
        obj.VisitComponents(ref serializer);
        writer = serializer.Writer;
    }

    public struct ComponentCounter : IComponentVisitor
    {
        public int DirtyCount;
        public bool IsNew;
        public void Visit(IComponent component)
        {
            if (IsNew || component.IsDirty) DirtyCount++;
        }
    }

    private ref struct ComponentSerializer : IComponentVisitor
    {
        public BitWriter Writer;
        public bool IsNew;
        public void Visit(IComponent comp)
        {
            if (!IsNew && !comp.IsDirty) return;

            Writer.WriteVarInt(Shared.Services.ComponentIdRegistry.GetId(comp.GetType()));

            long sizeFieldOffset = Writer.BitsWritten;
            Writer.WriteInt(0, 16);

            long startBits = Writer.BitsWritten;
            comp.WriteState(ref Writer);
            long endBits = Writer.BitsWritten;

            long payloadBits = endBits - startBits;
            Writer.PatchBits(sizeFieldOffset, (ulong)payloadBits, 16);
            comp.IsDirty = false;
        }
    }

    public void Read(ref BitReader reader, GameObject obj, GameObjectFields currentMask, List<(GameObject target, int propIdx, long refId)> unresolved)
    {
        int compCount = (int)reader.ReadVarInt();
        for (int i = 0; i < compCount; i++)
        {
            int componentId = (int)reader.ReadVarInt();
            int payloadBits = reader.ReadInt(16);

            var finder = new ComponentFinder { ComponentId = componentId };
            obj.VisitComponents(ref finder);

            if (finder.Found != null)
            {
                long startBits = reader.BitsRead;
                finder.Found.ReadState(ref reader);
                long actualRead = reader.BitsRead - startBits;
                if (actualRead < payloadBits) reader.SkipBits((int)(payloadBits - actualRead));
            }
            else
            {
                reader.SkipBits(payloadBits);
            }
        }
    }

    private struct ComponentFinder : IComponentVisitor
    {
        public int ComponentId;
        public IComponent? Found;
        public void Visit(IComponent component)
        {
            if (Found == null && Shared.Services.ComponentIdRegistry.GetId(component.GetType()) == ComponentId) Found = component;
        }
    }

    public void Skip(ref BitReader reader, GameObjectFields currentMask)
    {
        int compCount = (int)reader.ReadVarInt();
        for (int i = 0; i < compCount; i++)
        {
            reader.ReadVarInt(); // ComponentId
            int payloadBits = reader.ReadInt(16);
            reader.SkipBits(payloadBits);
        }
    }

    public int SnapshotStateSize => 0;
    public void SaveState(Span<byte> destination, IGameObject obj) { }
    public void Interpolate(IGameObject obj, ReadOnlySpan<byte> from, ReadOnlySpan<byte> to, double t) { }
}
