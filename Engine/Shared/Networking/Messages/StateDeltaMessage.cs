using System;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Utils;
using Shared.Services;

namespace Shared.Networking.Messages;

public class StateDeltaMessage : INetworkMessage
{
    private readonly ISnapshotSerializer? _serializer;
    public byte MessageTypeId => (byte)SnapshotMessageType.BitPackedDelta;

    // Serialization context
    public IEnumerable<IGameObject>? Objects { get; set; }
    public IDictionary<long, long>? LastSentVersions { get; set; }

    // Deserialization context
    public IDictionary<long, GameObject>? World { get; set; }
    public IObjectTypeManager? TypeManager { get; set; }
    public IObjectFactory? ObjectFactory { get; set; }

    public StateDeltaMessage(ISnapshotSerializer serializer)
    {
        _serializer = serializer;
    }

    public StateDeltaMessage() { } // Parameterless for DI if needed

    public void Write(ref BitWriter writer)
    {
        _serializer?.SerializeBitPackedDelta(ref writer, Objects!, LastSentVersions);
    }

    public void Read(ref BitReader reader)
    {
        _serializer?.DeserializeBitPacked(ref reader, World!, TypeManager!, ObjectFactory!);
    }
}
