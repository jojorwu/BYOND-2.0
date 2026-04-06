using System.Collections.Generic;
using System.Text.Json;
using Shared.Interfaces;
using Shared.Utils;

using Shared.Buffers;
namespace Shared.Networking.Messages;

public enum SnapshotMessageType : byte
{
    Full,
    Delta,
    Binary,
    BitPackedDelta,
    Json,
    Sound,
    StopSound,
    SyncCVars
}

public class SoundMessage : INetworkMessage
{
    public byte MessageTypeId => (byte)SnapshotMessageType.Sound;
    public SoundData Data { get; set; }

    public void Write(ref BitWriter writer)
    {
        writer.WriteString(Data.File);
        writer.WriteDouble(Data.Volume);
        writer.WriteDouble(Data.Pitch);
        writer.WriteBool(Data.Repeat);
        writer.WriteBool(Data.X.HasValue);
        if (Data.X.HasValue) writer.WriteZigZag(Data.X.Value);
        writer.WriteBool(Data.Y.HasValue);
        if (Data.Y.HasValue) writer.WriteZigZag(Data.Y.Value);
        writer.WriteBool(Data.Z.HasValue);
        if (Data.Z.HasValue) writer.WriteZigZag(Data.Z.Value);
        writer.WriteBool(Data.ObjectId.HasValue);
        if (Data.ObjectId.HasValue) writer.WriteVarInt(Data.ObjectId.Value);
        writer.WriteDouble(Data.Falloff);
    }

    public void Read(ref BitReader reader)
    {
        var sound = new SoundData();
        sound.File = reader.ReadString();
        sound.Volume = (float)reader.ReadDouble();
        sound.Pitch = (float)reader.ReadDouble();
        sound.Repeat = reader.ReadBool();
        if (reader.ReadBool()) sound.X = reader.ReadZigZag();
        if (reader.ReadBool()) sound.Y = reader.ReadZigZag();
        if (reader.ReadBool()) sound.Z = reader.ReadZigZag();
        if (reader.ReadBool()) sound.ObjectId = reader.ReadVarInt();
        sound.Falloff = (float)reader.ReadDouble();
        Data = sound;
    }
}

public class StopSoundMessage : INetworkMessage
{
    public byte MessageTypeId => (byte)SnapshotMessageType.StopSound;
    public string File { get; set; } = string.Empty;
    public long? ObjectId { get; set; }

    public void Write(ref BitWriter writer)
    {
        writer.WriteString(File);
        writer.WriteBool(ObjectId.HasValue);
        if (ObjectId.HasValue) writer.WriteVarInt(ObjectId.Value);
    }

    public void Read(ref BitReader reader)
    {
        File = reader.ReadString();
        if (reader.ReadBool()) ObjectId = reader.ReadVarInt();
    }
}

public class CVarSyncMessage : INetworkMessage
{
    public byte MessageTypeId => (byte)SnapshotMessageType.SyncCVars;
    public Dictionary<string, object> CVars { get; set; } = new();

    public void Write(ref BitWriter writer)
    {
        string json = JsonSerializer.Serialize(CVars);
        writer.WriteString(json);
    }

    public void Read(ref BitReader reader)
    {
        string json = reader.ReadString();
        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        if (data != null) CVars = data;
    }
}
