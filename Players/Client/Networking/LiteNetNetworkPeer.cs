using System.Threading.Tasks;
using System.Net;
using System.Collections.Generic;
using Shared;
using LiteNetLib;

namespace Client.Networking;

public class LiteNetNetworkPeer : INetworkPeer
{
    private readonly NetPeer _peer;
    public IDictionary<long, long> LastSentVersions { get; } = new Dictionary<long, long>();
    public string? Nickname { get; set; }
    public IPEndPoint? EndPoint => null;

    public LiteNetNetworkPeer(NetPeer peer)
    {
        _peer = peer;
    }

    public ValueTask SendAsync(string data)
    {
        var writer = new LiteNetLib.Utils.NetDataWriter();
        writer.Put(data);
        _peer.Send(writer, DeliveryMethod.ReliableOrdered);
        return ValueTask.CompletedTask;
    }

    public ValueTask SendAsync(byte[] data)
    {
        _peer.Send(data, DeliveryMethod.ReliableOrdered);
        return ValueTask.CompletedTask;
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> data)
    {
        _peer.Send(data.Span, DeliveryMethod.ReliableOrdered);
        return ValueTask.CompletedTask;
    }
}
