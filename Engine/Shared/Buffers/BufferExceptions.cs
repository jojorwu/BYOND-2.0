using System;

namespace Shared.Buffers;

/// <summary>
/// Base class for all buffer-related exceptions.
/// </summary>
public class BufferException : Exception
{
    public BufferException(string message) : base(message) { }
    public BufferException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a buffer operation exceeds its capacity.
/// </summary>
public sealed class BufferOverflowException : BufferException
{
    public long Capacity { get; }
    public long Requested { get; }

    public BufferOverflowException(long capacity, long requested)
        : base($"Buffer overflow. Capacity: {capacity}, Requested: {requested}")
    {
        Capacity = capacity;
        Requested = requested;
    }

    public BufferOverflowException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a read operation is attempted beyond the available data.
/// </summary>
public sealed class BufferUnderflowException : BufferException
{
    public BufferUnderflowException(string message) : base(message) { }
}

/// <summary>
/// Thrown when an invalid offset is provided to a buffer.
/// </summary>
public sealed class InvalidBufferOffsetException : BufferException
{
    public InvalidBufferOffsetException(string message) : base(message) { }
}
