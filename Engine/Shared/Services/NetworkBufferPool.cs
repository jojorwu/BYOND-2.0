using System;
using System.Buffers;

namespace Shared.Services;

public interface INetworkBufferPool
{
    byte[] Rent(int size);
    void Return(byte[] buffer);
}

public class NetworkBufferPool : INetworkBufferPool
{
    private readonly ArrayPool<byte> _pool = ArrayPool<byte>.Shared;

    public byte[] Rent(int size) => _pool.Rent(size);
    public void Return(byte[] buffer) => _pool.Return(buffer);
}
