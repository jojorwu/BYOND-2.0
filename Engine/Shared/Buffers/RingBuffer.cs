using System;
using System.Collections;
using System.Collections.Generic;
using Shared.Interfaces;

namespace Shared.Buffers;

/// <summary>
/// A circular buffer implementation for efficient fixed-size storage and FIFO access.
/// </summary>
/// <typeparam name="T">The type of elements in the buffer.</typeparam>
public class RingBuffer<T> : IBuffer, IEnumerable<T>
{
    private T[] _buffer;
    private int _head;
    private int _tail;
    private int _count;
    private readonly IDiagnosticBus? _diagnosticBus;

    /// <summary>
    /// Initializes a new instance of the <see cref="RingBuffer{T}"/> class with the specified capacity.
    /// </summary>
    /// <param name="capacity">The maximum number of elements the buffer can hold.</param>
    /// <param name="diagnosticBus">The diagnostic bus to publish metrics to.</param>
    public RingBuffer(int capacity, IDiagnosticBus? diagnosticBus = null)
    {
        _buffer = new T[capacity];
        _diagnosticBus = diagnosticBus;
    }

    /// <summary>
    /// Gets the number of elements currently in the buffer.
    /// </summary>
    public int Count => _count;

    /// <inheritdoc />
    public long Capacity => _buffer.Length;

    /// <inheritdoc />
    public long Position => _tail;

    /// <inheritdoc />
    public int SlabCount => 1;

    /// <inheritdoc />
    public long TotalAllocatedBytes => 0; // Managed objects

    /// <summary>
    /// Adds an item to the end of the buffer. If the buffer is full, the oldest item is overwritten.
    /// </summary>
    /// <param name="item">The item to add.</param>
    public void Add(T item)
    {
        lock (_buffer)
        {
            if (_count == (int)Capacity)
            {
                _head = (_head + 1) % (int)Capacity;
            }
            else
            {
                _count++;
            }

            _buffer[_tail] = item;
            _tail = (_tail + 1) % (int)Capacity;
        }
    }

    /// <summary>
    /// Adds a collection of items to the buffer.
    /// </summary>
    /// <param name="items">The items to add.</param>
    public void Add(ReadOnlySpan<T> items)
    {
        lock (_buffer)
        {
            foreach (var item in items)
            {
                if (_count == (int)Capacity)
                {
                    _head = (_head + 1) % (int)Capacity;
                }
                else
                {
                    _count++;
                }

                _buffer[_tail] = item;
                _tail = (_tail + 1) % (int)Capacity;
            }
        }
    }

    /// <summary>
    /// Attempts to remove and return the oldest item in the buffer.
    /// </summary>
    /// <param name="item">When this method returns, contains the oldest item if the buffer was not empty; otherwise, the default value.</param>
    /// <returns>True if an item was successfully removed; otherwise, false.</returns>
    public bool TryPop(out T item)
    {
        lock (_buffer)
        {
            if (_count == 0)
            {
                item = default!;
                return false;
            }
            item = _buffer[_head];
            _buffer[_head] = default!;
            _head = (_head + 1) % (int)Capacity;
            _count--;
            return true;
        }
    }

    /// <summary>
    /// Removes and returns the oldest item in the buffer.
    /// </summary>
    /// <returns>The oldest item.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the buffer is empty.</exception>
    public T PopOldest()
    {
        if (!TryPop(out var item)) throw new InvalidOperationException("Buffer is empty");
        return item;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the items in the buffer in order from oldest to newest.
    /// </summary>
    /// <returns>An enumerator for the buffer.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
        {
            yield return _buffer[(_head + i) % Capacity];
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public void Shrink() { }

    /// <inheritdoc />
    public ReadOnlySpan<byte> GetSegmentAsSpan(long offset, int length) => throw new NotSupportedException();

    /// <inheritdoc />
    public Span<byte> GetMutableSegmentAsSpan(long offset, int length) => throw new NotSupportedException();

    /// <inheritdoc />
    public void Reset()
    {
        lock (_buffer)
        {
            _head = 0;
            _tail = 0;
            _count = 0;
            Array.Clear(_buffer);
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object> GetDiagnosticInfo()
    {
        var info = new Dictionary<string, object>
        {
            ["Capacity"] = Capacity,
            ["Count"] = Count,
            ["Position"] = Position
        };

        _diagnosticBus?.Publish("Buffer", $"RingBuffer<{typeof(T).Name}> Stats", info, (m, state) =>
        {
            foreach (var kvp in state) m.Add(kvp.Key, kvp.Value.ToString() ?? string.Empty);
        });

        return info;
    }
}
