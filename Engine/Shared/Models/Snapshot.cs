using System;
using System.Collections.Generic;

namespace Shared.Models;

public class Snapshot
{
    public double Timestamp;
    public long[] ObjectIds = Array.Empty<long>();
    public byte[] StateBuffer = Array.Empty<byte>();
    public int Count;
    public int StateStride;

    public void Reset()
    {
        Timestamp = 0;
        Count = 0;
    }

    public ReadOnlySpan<byte> GetStateSpan(long id)
    {
        int index = Array.BinarySearch(ObjectIds, 0, Count, id);
        if (index >= 0)
        {
            return StateBuffer.AsSpan(index * StateStride, StateStride);
        }
        return ReadOnlySpan<byte>.Empty;
    }
}
