using System;
using System.Threading.Tasks;
using Shared.Interfaces;
using Shared.Utils;
using Shared.Networking;

namespace Shared.Services;

public class NetworkSender : INetworkSender
{
    private readonly INetworkBufferPool _bufferPool;

    public NetworkSender(INetworkBufferPool bufferPool)
    {
        _bufferPool = bufferPool;
    }

    public async ValueTask SendAsync(INetworkPeer peer, INetworkMessage message)
    {
        int bufferSize = 2048;
        while (true)
        {
            byte[] buffer = _bufferPool.Rent(bufferSize);
            try
            {
                var writer = new BitWriter(buffer);
                writer.WriteByte((byte)NetworkMessageType.Message);
                writer.WriteByte(message.MessageTypeId);
                message.Write(ref writer);

                await peer.SendAsync(new ReadOnlyMemory<byte>(buffer, 0, writer.BytesWritten));
                return;
            }
            catch (IndexOutOfRangeException)
            {
                bufferSize *= 2;
                if (bufferSize > 1024 * 1024) throw; // Max 1MB
            }
            finally
            {
                _bufferPool.Return(buffer);
            }
        }
    }
}
