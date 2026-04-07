using System;
using System.Buffers;
using Shared.Attributes;
using Shared.Interfaces;

namespace Shared.Buffers;

/// <summary>
/// Defines a specialized pool for renting and returning network buffers to minimize byte array allocations.
/// </summary>
public interface INetworkBufferPool
{
    /// <summary>
    /// Rents a buffer of at least the specified size.
    /// </summary>
    /// <param name="size">The minimum required size in bytes.</param>
    /// <returns>A byte array of at least the requested size.</returns>
    byte[] Rent(int size);

    /// <summary>
    /// Returns a previously rented buffer to the pool.
    /// </summary>
    /// <param name="buffer">The buffer to return.</param>
    void Return(byte[] buffer);
}

/// <summary>
/// Standard implementation of <see cref="INetworkBufferPool"/> using <see cref="ArrayPool{T}.Shared"/>.
/// </summary>
[EngineService(typeof(INetworkBufferPool))]
public class NetworkBufferPool : INetworkBufferPool
{
    private readonly ArrayPool<byte> _pool = ArrayPool<byte>.Shared;
    private readonly IDiagnosticBus? _diagnosticBus;
    private long _rentCount;
    private long _returnCount;

    public NetworkBufferPool(IDiagnosticBus? diagnosticBus = null)
    {
        _diagnosticBus = diagnosticBus;
    }

    public byte[] Rent(int size)
    {
        System.Threading.Interlocked.Increment(ref _rentCount);
        _diagnosticBus?.Publish("Network", "Buffer Rented", size, (m, s) => m.Add("Size", s));
        return _pool.Rent(size);
    }

    public void Return(byte[] buffer)
    {
        System.Threading.Interlocked.Increment(ref _returnCount);
        _pool.Return(buffer);
    }
}
