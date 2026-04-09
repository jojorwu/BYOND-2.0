using System;
using System.Buffers;
using System.Collections.Generic;

namespace Shared.Buffers;

/// <summary>
/// A simple internal helper to build a <see cref="ReadOnlySequence{T}"/> from multiple <see cref="ReadOnlyMemory{T}"/> segments.
/// </summary>
internal sealed class SequenceBuilder
{
    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory, long runningIndex)
        {
            Memory = memory;
            RunningIndex = runningIndex;
        }

        public void SetNext(Segment? next) => Next = next;
    }

    private Segment? _first;
    private Segment? _last;
    private long _totalLength;

    /// <summary>
    /// Adds a memory segment to the sequence.
    /// </summary>
    /// <param name="memory">The memory block to add.</param>
    public void Add(ReadOnlyMemory<byte> memory)
    {
        if (memory.IsEmpty) return;

        var segment = new Segment(memory, _totalLength);
        if (_first == null)
        {
            _first = segment;
            _last = segment;
        }
        else
        {
            _last!.SetNext(segment);
            _last = segment;
        }

        _totalLength += memory.Length;
    }

    /// <summary>
    /// Builds the final <see cref="ReadOnlySequence{byte}"/>.
    /// </summary>
    /// <returns>The constructed sequence.</returns>
    public ReadOnlySequence<byte> Build()
    {
        if (_first == null) return ReadOnlySequence<byte>.Empty;
        return new ReadOnlySequence<byte>(_first, 0, _last!, _last!.Memory.Length);
    }
}
