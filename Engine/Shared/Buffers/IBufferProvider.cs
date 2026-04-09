using Shared.Attributes;
using Shared.Interfaces;

namespace Shared.Buffers;

/// <summary>
/// Provides factory methods for creating and managing buffer instances.
/// </summary>
public interface IBufferProvider
{
    /// <summary>
    /// Creates a new <see cref="SnapshotBuffer"/> with default settings.
    /// </summary>
    SnapshotBuffer CreateSnapshotBuffer();

    /// <summary>
    /// Creates a new <see cref="ArenaAllocator"/> with default settings.
    /// </summary>
    ArenaAllocator CreateArenaAllocator();

    /// <summary>
    /// Creates a <see cref="DoubleBuffer{T}"/> for the specified state type.
    /// </summary>
    DoubleBuffer<T> CreateDoubleBuffer<T>(T initialState) where T : class;
}

/// <summary>
/// Default implementation of the buffer provider.
/// </summary>
[EngineService(typeof(IBufferProvider))]
public sealed class BufferProvider : IBufferProvider
{
    private readonly ISlabAllocator _slabAllocator;
    private readonly IDiagnosticBus? _diagnosticBus;

    public BufferProvider(ISlabAllocator slabAllocator, IDiagnosticBus? diagnosticBus = null)
    {
        _slabAllocator = slabAllocator;
        _diagnosticBus = diagnosticBus;
    }

    /// <inheritdoc />
    public SnapshotBuffer CreateSnapshotBuffer()
    {
        return new SnapshotBuffer(_slabAllocator, _diagnosticBus);
    }

    /// <inheritdoc />
    public ArenaAllocator CreateArenaAllocator()
    {
        return new ArenaAllocator(_slabAllocator, _diagnosticBus);
    }

    /// <inheritdoc />
    public DoubleBuffer<T> CreateDoubleBuffer<T>(T initialState) where T : class
    {
        return new DoubleBuffer<T>(initialState, _diagnosticBus);
    }
}
