using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shared.Buffers;

namespace Tests;

[TestFixture]
public class BufferArchitectureTests
{
    [Test]
    public void DefaultSlabAllocator_AllocatesCorrectly()
    {
        var allocator = new DefaultSlabAllocator();
        var slab = allocator.Allocate(1024, pinned: false);

        Assert.That(slab, Is.Not.Null);
        Assert.That(slab.Capacity, Is.GreaterThanOrEqualTo(1024));
        Assert.That(slab.IsFromPool, Is.True);
        Assert.That(slab.IsOversized, Is.False);

        allocator.Return(slab);
    }

    [Test]
    public void DefaultSlabAllocator_Oversized_BypassesPool()
    {
        var allocator = new DefaultSlabAllocator();
        var slab = allocator.Allocate(1024, pinned: false, isOversized: true);

        Assert.That(slab.IsFromPool, Is.False);
        Assert.That(slab.IsOversized, Is.True);

        allocator.Return(slab);
    }

    [Test]
    public void BitReader_SegmentTransitions_WorkCorrectly()
    {
        // Create 3 segments of 2 bytes each
        var mem1 = new ReadOnlyMemory<byte>(new byte[] { 0xAA, 0xBB }); // 10101010, 10111011
        var mem2 = new ReadOnlyMemory<byte>(new byte[] { 0xCC, 0xDD }); // 11001100, 11011101
        var mem3 = new ReadOnlyMemory<byte>(new byte[] { 0xEE, 0xFF }); // 11101110, 11111111

        var segments = new ReadOnlyMemory<byte>[] { mem1, mem2, mem3 };
        var reader = new BitReader(segments.AsSpan());

        // Read 12 bits from first segment
        // AA (8 bits) + High 4 of BB (0xB)
        // 10101010 1011
        var val1 = reader.ReadBits(12);
        Assert.That(val1, Is.EqualTo(0xAAB));

        // Read remaining 4 bits of BB + 8 bits of CC + 4 bits of DD
        // 1011 (Low 4 of BB) -> 0xB
        // 11001100 (CC) -> 0xCC
        // 1101 (High 4 of DD) -> 0xD
        // Total: 4 + 8 + 4 = 16 bits
        var val2 = reader.ReadBits(16);
        Assert.That(val2, Is.EqualTo(0xBCCD));

        // Read remaining 4 bits of DD + 8 bits of EE
        // 1101 (Low 4 of DD) -> 0xD
        // 11101110 (EE) -> 0xEE
        var val3 = reader.ReadBits(12);
        Assert.That(val3, Is.EqualTo(0xDEE));

        Assert.That(reader.BitsRead, Is.EqualTo(12 + 16 + 12));
    }

    [Test]
    public void SnapshotBuffer_SegmentProvider_IteratesCorrectly()
    {
        using var buffer = new SnapshotBuffer(defaultSize: 4); // Small slabs for testing

        // Write to multiple slabs
        buffer.AcquireSegment(3, out _); // Slab 0
        buffer.AcquireSegment(3, out _); // Slab 1
        buffer.AcquireSegment(3, out _); // Slab 2

        var provider = buffer.GetSegments();
        Assert.That(provider.Count, Is.EqualTo(3));

        var segmentList = new List<ReadOnlyMemory<byte>>();
        foreach (var segment in provider)
        {
            segmentList.Add(segment);
            Assert.That(segment.Length, Is.EqualTo(3));
        }

        Assert.That(segmentList.Count, Is.EqualTo(3));
    }

    [Test]
    public void BitWriter_BufferWriterIntegration_Works()
    {
        using var snapshotBuffer = new SnapshotBuffer(defaultSize: 8);
        var writer = new BitWriter(snapshotBuffer);

        // Write across segments
        writer.WriteBits(0xAAAA_AAAA, 32); // 4 bytes
        writer.WriteBits(0xBBBB_BBBB, 32); // 4 bytes. Slab 0 full.
        writer.WriteBits(0xCCCC_CCCC, 32); // 4 bytes. Slab 1.

        writer.Flush();

        var segments = snapshotBuffer.GetSegments();
        Assert.That(segments.Count, Is.EqualTo(2));
        Assert.That(segments[0].Length, Is.EqualTo(8));
        Assert.That(segments[1].Length, Is.EqualTo(4));

        var reader = new BitReader(segments);
        Assert.That(reader.ReadBits(32), Is.EqualTo(0xAAAA_AAAA));
        Assert.That(reader.ReadBits(32), Is.EqualTo(0xBBBB_BBBB));
        Assert.That(reader.ReadBits(32), Is.EqualTo(0xCCCC_CCCC));
    }

    [Test]
    public void BitReader_ReadOnlySequence_Works()
    {
        var data = new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66 };
        var sequence = new ReadOnlySequence<byte>(data);
        var reader = new BitReader(sequence);

        Assert.That(reader.ReadBits(16), Is.EqualTo(0x1122));
        Assert.That(reader.ReadBits(16), Is.EqualTo(0x3344));
        Assert.That(reader.ReadBits(16), Is.EqualTo(0x5566));
    }

    [Test]
    public void BitWriter_PatchBits_CrossSegments_Works()
    {
        // Use a small default size to force segment transitions
        using var snapshotBuffer = new SnapshotBuffer(defaultSize: 4);
        var writer = new BitWriter(snapshotBuffer);

        // Write 4 bits: 1010
        writer.WriteBits(0xA, 4);

        // Record position for patching (4 bits in)
        long patchPos = writer.BitsWritten;

        // Write 8 bits placeholder: 0000 0000
        writer.WriteBits(0, 8);

        // Force a segment transition by writing enough data to fill the 4-byte slab
        // Current bits: 4 + 8 = 12. Slab has 32 bits.
        // Write 24 more bits.
        writer.WriteBits(0xFFFFFF, 24);

        // Now we should be in a new segment
        Assert.That(snapshotBuffer.SlabCount, Is.GreaterThan(1));
        // We wrote 12 bits, then EnsureCapacity(24) triggered a byte-aligned Flush() to 16 bits.
        // Then 24 more bits were written. 16 + 24 = 40.
        Assert.That(writer.BitsWritten, Is.EqualTo(40));

        // Patch the 8-bit placeholder with 0xBB (1011 1011)
        writer.PatchBits(patchPos, 0xBB, 8);

        writer.Flush();

        // Verify with reader
        var reader = new BitReader(snapshotBuffer.GetSegments());
        Assert.That(reader.ReadBits(4), Is.EqualTo(0xA));
        Assert.That(reader.ReadBits(8), Is.EqualTo(0xBB));

        // Skip the 4-bit alignment gap (12 to 16)
        reader.SkipBits(4);
        Assert.That(reader.ReadBits(24), Is.EqualTo(0xFFFFFF));
    }

    [Test]
    public void SnapshotBuffer_FindSlabIndex_WorksAcrossBoundaries()
    {
        using var buffer = new SnapshotBuffer(defaultSize: 1024);
        buffer.AcquireSegment(1024, out _); // Fill first slab
        buffer.AcquireSegment(1024, out _); // Fill second slab

        // Offset 1023 should be in slab 0
        // Offset 1024 should be in slab 1
        // (Assuming BaseOffset 0, 1024)

        var span0 = buffer.GetSegmentAsSpan(1023, 1);
        var span1 = buffer.GetSegmentAsSpan(1024, 1);

        Assert.That(span0.Length, Is.EqualTo(1));
        Assert.That(span1.Length, Is.EqualTo(1));
    }

    [Test]
    public void SnapshotBuffer_CopyTo_Works()
    {
        using var buffer = new SnapshotBuffer(defaultSize: 8);
        var writer = new BitWriter(buffer);
        writer.WriteBits(0x1122334455667788, 64); // 8 bytes - Slab 0 full
        writer.WriteBits(0xAABBCCDD, 32); // 4 bytes - Slab 1
        writer.Flush();

        using var ms = new System.IO.MemoryStream();
        buffer.CopyTo(ms);

        var data = ms.ToArray();
        Assert.That(data.Length, Is.EqualTo(12));
        Assert.That(data[0], Is.EqualTo(0x11));
        Assert.That(data[7], Is.EqualTo(0x88));
        Assert.That(data[8], Is.EqualTo(0xAA));
        Assert.That(data[11], Is.EqualTo(0xDD));

        byte[] destSpan = new byte[12];
        buffer.CopyTo(destSpan.AsSpan());
        Assert.That(destSpan, Is.EqualTo(data));
    }

    [Test]
    public void BitWriter_WriteBits_FromReader_Works()
    {
        var data = new byte[] { 0xAA, 0xBB, 0xCC };
        var reader = new BitReader(data);
        reader.ReadBits(4); // Skip 4 bits (A)

        using var buffer = new SnapshotBuffer(defaultSize: 1024);
        var writer = new BitWriter(buffer);
        writer.WriteBits(0xF, 4); // Write F
        writer.WriteBits(ref reader, 12); // Write remaining A (4) + BB (8)
        writer.Flush();

        var result = new BitReader(buffer.GetSegments());
        Assert.That(result.ReadBits(16), Is.EqualTo(0xFABB));
    }

    [Test]
    public void SnapshotBuffer_Slice_Works()
    {
        using var buffer = new SnapshotBuffer(defaultSize: 4);
        buffer.Advance(4);
        buffer.GetSpan(4)[0] = 0x11;
        buffer.Advance(4);
        buffer.GetSpan(4)[0] = 0x22;
        buffer.Advance(4);

        var slice = buffer.Slice(4, 8);
        Assert.That(slice.Length, Is.EqualTo(8));

        var reader = new BitReader(slice);
        Assert.That(reader.ReadByte(), Is.EqualTo(0x11));
        reader.SkipBits(24);
        Assert.That(reader.ReadByte(), Is.EqualTo(0x22));
    }
}
