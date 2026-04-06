using System;
using System.Threading.Tasks;
using Shared.Interfaces;
using Shared.Utils;
using Shared.Networking;
using Shared.Attributes;

using Shared.Buffers;
namespace Shared.Services;

[EngineService(typeof(INetworkSender))]
public class NetworkSender : INetworkSender
{
    private readonly INetworkBufferPool _bufferPool;
    private const int MaxBufferSize = 16 * 1024 * 1024; // Increased to 16MB

    public NetworkSender(INetworkBufferPool bufferPool)
    {
        _bufferPool = bufferPool;
    }

    public async ValueTask SendAsync(INetworkPeer peer, INetworkMessage message)
    {
        int bufferSize = 4096; // Start slightly larger
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
                // Exponential growth
                bufferSize *= 2;
                if (bufferSize > MaxBufferSize)
                {
                    throw new InvalidOperationException($"Network message too large: {bufferSize} exceeds {MaxBufferSize}");
                }
            }
            finally
            {
                _bufferPool.Return(buffer);
            }
        }
    }
}
