using System;
using System.Collections;
using System.Collections.Generic;

namespace Shared.Buffers;

/// <summary>
/// A circular buffer implementation for efficient fixed-size storage and FIFO access.
/// </summary>
/// <typeparam name="T">The type of elements in the buffer.</typeparam>
public class RingBuffer<T> : IEnumerable<T>
{
    private T[] _buffer;
    private int _head;
    private int _tail;
    private int _count;

    /// <summary>
    /// Initializes a new instance of the <see cref="RingBuffer{T}"/> class with the specified capacity.
    /// </summary>
    /// <param name="capacity">The maximum number of elements the buffer can hold.</param>
    public RingBuffer(int capacity)
    {
        _buffer = new T[capacity];
    }

    /// <summary>
    /// Gets the number of elements currently in the buffer.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets the maximum capacity of the buffer.
    /// </summary>
    public int Capacity => _buffer.Length;

    /// <summary>
    /// Adds an item to the end of the buffer. If the buffer is full, the oldest item is overwritten.
    /// </summary>
    /// <param name="item">The item to add.</param>
    public void Add(T item)
    {
        lock (_buffer)
        {
            if (_count == Capacity)
            {
                _head = (_head + 1) % Capacity;
            }
            else
            {
                _count++;
            }

            _buffer[_tail] = item;
            _tail = (_tail + 1) % Capacity;
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
                if (_count == Capacity)
                {
                    _head = (_head + 1) % Capacity;
                }
                else
                {
                    _count++;
                }

                _buffer[_tail] = item;
                _tail = (_tail + 1) % Capacity;
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
            _head = (_head + 1) % Capacity;
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
}
