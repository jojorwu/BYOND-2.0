using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shared;
using Shared.Interfaces;
using Shared.Services;
using Shared.Utils;

using Shared.Buffers;
namespace Core
{
    public class GameStateSnapshotter : EngineService, IGameStateSnapshotter
    {
        private readonly BinarySnapshotService _binarySnapshotService;
        private readonly ReactiveStateSystem? _reactiveSystem;
        private readonly IJobSystem? _jobSystem;

        public GameStateSnapshotter(BinarySnapshotService binarySnapshotService, IJobSystem? jobSystem = null, ReactiveStateSystem? reactiveSystem = null)
        {
            _binarySnapshotService = binarySnapshotService;
            _jobSystem = jobSystem;
            _reactiveSystem = reactiveSystem;
        }

        public string GetSnapshot(IGameState gameState)
        {
            using (gameState.ReadLock())
            {
                var snapshot = new
                {
                    gameState.Map,
                    gameState.GameObjects
                };
                return JsonSerializer.Serialize(snapshot);
            }
        }

        public string GetSnapshot(IGameState gameState, MergedRegion region)
        {
            using (gameState.ReadLock())
            {
                var snapshot = new
                {
                    GameObjects = region.GetGameObjects(gameState)
                };
                return JsonSerializer.Serialize(snapshot);
            }
        }

        public string GetSnapshot(IGameState gameState, Region region)
        {
            using (gameState.ReadLock())
            {
                var snapshot = new
                {
                    GameObjects = region.GetGameObjects(gameState)
                };
                return JsonSerializer.Serialize(snapshot);
            }
        }

        public byte[] GetBinarySnapshot(IGameState gameState)
        {
            using (gameState.ReadLock())
            {
                int objectCount = gameState.GameObjects.Count;
                if (objectCount == 0) return Array.Empty<byte>();

                var objects = ArrayPool<GameObject>.Shared.Rent(objectCount);
                try
                {
                    int actualCount = 0;
                    foreach (var kvp in gameState.GameObjects)
                    {
                        objects[actualCount++] = kvp.Value;
                        if (actualCount >= objectCount) break;
                    }

                    if (_jobSystem != null && actualCount > 1024)
                    {
                        int workerCount = Math.Max(1, Environment.ProcessorCount);
                        int batchSize = (actualCount + workerCount - 1) / workerCount;
                        int numBatches = (actualCount + batchSize - 1) / batchSize;

                        var segmentTasks = new byte[numBatches][];
                        var finalObjects = objects; // Capture for lambda

                        _jobSystem.ForEachAsync(Enumerable.Range(0, numBatches), b =>
                        {
                            int start = b * batchSize;
                            int length = Math.Min(batchSize, actualCount - start);
                            if (length <= 0) return;

                            var batch = new GameObject[length];
                            Array.Copy(finalObjects, start, batch, 0, length);

                            int bufferSize = length * 128;
                            var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                            try
                            {
                                var writer = new BitWriter(buffer);
                                _binarySnapshotService.SerializeBitPackedDelta(ref writer, batch, null);
                                segmentTasks[b] = buffer.AsSpan(0, writer.BytesWritten).ToArray();
                            }
                            finally { ArrayPool<byte>.Shared.Return(buffer); }
                        }).GetAwaiter().GetResult();

                        int totalSize = segmentTasks.Where(s => s != null).Sum(s => s.Length);
                        byte[] result = new byte[totalSize];
                        int offset = 0;
                        foreach (var segment in segmentTasks)
                        {
                            if (segment == null) continue;
                            segment.CopyTo(result, offset);
                            offset += segment.Length;
                        }
                        return result;
                    }

                    int bufSize = Math.Max(65536, actualCount * 64);
                    while (true)
                    {
                        var buffer = ArrayPool<byte>.Shared.Rent(bufSize);
                        try
                        {
                            var writer = new BitWriter(buffer);
                            var array = new GameObject[actualCount];
                            Array.Copy(objects, 0, array, 0, actualCount);
                            _binarySnapshotService.SerializeBitPackedDelta(ref writer, array, null);
                            return buffer.AsSpan(0, writer.BytesWritten).ToArray();
                        }
                        catch (IndexOutOfRangeException)
                        {
                            bufSize *= 2;
                            if (bufSize > 100 * 1024 * 1024) throw;
                        }
                        finally { ArrayPool<byte>.Shared.Return(buffer); }
                    }
                }
                finally { ArrayPool<GameObject>.Shared.Return(objects); }
            }
        }

        public byte[] GetSparseBinarySnapshot(IGameState gameState)
        {
            if (_reactiveSystem != null)
            {
                int count = _reactiveSystem.BatchCount;
                if (count == 0) return Array.Empty<byte>();

                var batches = new List<ReactiveStateSystem.DeltaBatch>(count);
                _reactiveSystem.ConsumeBatches(batches);

                int bufferSize = Math.Max(4096, batches.Count * 64);
                while (true)
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                    try
                    {
                        int bytesWritten = _binarySnapshotService.SerializeBatches(buffer, batches, out bool truncated);
                        if (!truncated)
                        {
                            return buffer.AsSpan(0, bytesWritten).ToArray();
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                    bufferSize *= 2;
                }
            }

            using (gameState.ReadLock())
            {
                var objects = gameState.GetDirtyObjects().ToList();
                if (objects.Count == 0) return Array.Empty<byte>();

                int bufferSize = Math.Max(4096, objects.Count * 64);
                while (true)
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                    try
                    {
                        var writer = new BitWriter(buffer);
                        _binarySnapshotService.SerializeBitPackedDelta(ref writer, objects, null);
                        return buffer.AsSpan(0, writer.BytesWritten).ToArray();
                    }
                    catch (IndexOutOfRangeException)
                    {
                        bufferSize *= 2;
                        if (bufferSize > 100 * 1024 * 1024) throw;
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
            }
        }

        public byte[] GetBinarySnapshot(IGameState gameState, MergedRegion mergedRegion)
        {
            using (gameState.ReadLock())
            {
                var objects = mergedRegion.GetGameObjects(gameState).ToList();
                int bufferSize = Math.Max(65536, objects.Count * 64);
                while (true)
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                    try
                    {
                        var writer = new BitWriter(buffer);
                        _binarySnapshotService.SerializeBitPackedDelta(ref writer, objects, null);
                        return buffer.AsSpan(0, writer.BytesWritten).ToArray();
                    }
                    catch (IndexOutOfRangeException)
                    {
                        bufferSize *= 2;
                        if (bufferSize > 100 * 1024 * 1024) throw;
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
            }
        }
    }
}
