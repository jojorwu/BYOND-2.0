using Shared.Interfaces;
using Shared.Utils;

namespace Shared.Networking.Messages;

public enum ClientMessageType : byte
{
    Ping,
    Command,
    Input
}

public class ClientPingMessage : INetworkMessage
{
    public byte MessageTypeId => (byte)ClientMessageType.Ping;
    public double Timestamp { get; set; }

    public void Write(ref BitWriter writer) => writer.WriteDouble(Timestamp);
    public void Read(ref BitReader reader) => Timestamp = reader.ReadDouble();
}

public class ClientCommandMessage : INetworkMessage
{
    public byte MessageTypeId => (byte)ClientMessageType.Command;
    public string Command { get; set; } = string.Empty;

    public void Write(ref BitWriter writer) => writer.WriteString(Command);
    public void Read(ref BitReader reader) => Command = reader.ReadString();
}

public enum ClientInputType : byte
{
    Move,
    Interact,
    Use
}

public class ClientInputMessage : INetworkMessage
{
    public byte MessageTypeId => (byte)ClientMessageType.Input;
    public ClientInputType InputType { get; set; }
    public float X { get; set; }
    public float Y { get; set; }

    public void Write(ref BitWriter writer)
    {
        writer.WriteByte((byte)InputType);
        writer.WriteFloat(X);
        writer.WriteFloat(Y);
    }

    public void Read(ref BitReader reader)
    {
        InputType = (ClientInputType)reader.ReadByte();
        X = reader.ReadFloat();
        Y = reader.ReadFloat();
    }
}
