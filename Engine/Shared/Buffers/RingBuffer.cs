using System;
using System.Collections;
using System.Collections.Generic;

namespace Shared.Buffers;

public class RingBuffer<T> : IEnumerable<T>
{
    private T[] _buffer;
    private int _head;
    private int _tail;
    private int _count;

    public RingBuffer(int capacity)
    {
        _buffer = new T[capacity];
    }

    public int Count => _count;
    public int Capacity => _buffer.Length;

    public void Add(T item)
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

    public T PopOldest()
    {
        if (_count == 0) throw new InvalidOperationException("Buffer is empty");
        T item = _buffer[_head];
        _buffer[_head] = default!;
        _head = (_head + 1) % Capacity;
        _count--;
        return item;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
        {
            yield return _buffer[(_head + i) % Capacity];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
