using System;
using System.Threading.Tasks;
using Shared.Interfaces;
using Shared.Utils;

namespace Shared.Services;

public interface INetworkSender
{
    ValueTask SendAsync(INetworkPeer peer, INetworkMessage message);
}

public class NetworkSender : INetworkSender
{
    public async ValueTask SendAsync(INetworkPeer peer, INetworkMessage message)
    {
        byte[] buffer = new byte[1024]; // Reusable pooled buffer would be better
        var writer = new BitWriter(buffer);
        writer.WriteBits(message.MessageTypeId, 8);
        message.Write(ref writer);

        byte[] result = new byte[writer.BytesWritten];
        buffer.AsSpan(0, writer.BytesWritten).CopyTo(result);

        await peer.SendAsync(result);
    }
}
