using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace Shared.Buffers;

internal sealed class BufferSlab : IDisposable
{
    public readonly byte[] Data;
    public readonly int Capacity;
    public readonly bool IsFromPool;
    public readonly bool IsOversized;
    public int Offset;
    private GCHandle _handle;
    public readonly unsafe byte* Ptr;

    public unsafe BufferSlab(int size, bool fromPool, bool pinned, bool isOversized = false)
    {
        Capacity = size;
        IsFromPool = fromPool;
        IsOversized = isOversized;
        Offset = 0;

        if (fromPool)
        {
            Data = ArrayPool<byte>.Shared.Rent(size);
        }
        else
        {
            Data = GC.AllocateUninitializedArray<byte>(size, pinned: pinned);
        }

        if (pinned)
        {
            _handle = GCHandle.Alloc(Data, GCHandleType.Pinned);
            Ptr = (byte*)_handle.AddrOfPinnedObject();
        }
        else
        {
            Ptr = null;
        }
    }

    public void Dispose()
    {
        if (_handle.IsAllocated)
        {
            _handle.Free();
        }

        if (IsFromPool)
        {
            ArrayPool<byte>.Shared.Return(Data);
        }
    }
}
