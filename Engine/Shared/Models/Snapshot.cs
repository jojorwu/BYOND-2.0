using System;
using System.Collections.Generic;

namespace Shared.Models;

public class Snapshot
{
    public double Timestamp;
    public long[] ObjectIds = Array.Empty<long>();
    public byte[] StateBuffer = Array.Empty<byte>(); // Layout: [FieldHandler1 States][FieldHandler2 States]...
    public int Count;
    public int[] HandlerOffsets = Array.Empty<int>();

    public void Reset()
    {
        Timestamp = 0;
        Count = 0;
    }

}
