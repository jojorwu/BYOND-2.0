namespace Shared.Networking;

public enum NetworkMessageType : byte
{
    Snapshot = 0x01,
    Message = 0x02,
    Control = 0x03
}
