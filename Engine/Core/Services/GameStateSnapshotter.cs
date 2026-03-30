using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text.Json;
using Shared;
using Shared.Services;

namespace Core
{
    public class GameStateSnapshotter : EngineService, IGameStateSnapshotter
    {
        private readonly BinarySnapshotService _binarySnapshotService;
        private readonly ReactiveStateSystem? _reactiveSystem;

        public GameStateSnapshotter(BinarySnapshotService binarySnapshotService, ReactiveStateSystem? reactiveSystem = null)
        {
            _binarySnapshotService = binarySnapshotService;
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
                var objects = gameState.GameObjects.Values;
                int bufferSize = Math.Max(65536, objects.Count * 64);
                while (true)
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                    try
                    {
                        int bytesWritten = _binarySnapshotService.SerializeBitPackedDelta(buffer, objects, null, out bool truncated);
                        if (!truncated)
                        {
                            byte[] result = new byte[bytesWritten];
                            buffer.AsSpan(0, bytesWritten).CopyTo(result);
                            return result;
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                    bufferSize *= 2;
                }
            }
        }

        public byte[] GetSparseBinarySnapshot(IGameState gameState)
        {
            // If we have a reactive system, we should ideally use BitPacked as well
            // But for now let's focus on the primary binary snapshot which is used for full/regional updates

            if (_reactiveSystem != null)
            {
                var batches = _reactiveSystem.ConsumeBatches().ToList();
                if (batches.Count == 0) return Array.Empty<byte>();

                int bufferSize = Math.Max(4096, batches.Count * 64);
                while (true)
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                    try
                    {
                        int bytesWritten = _binarySnapshotService.SerializeBatches(buffer, batches, out bool truncated);
                        if (!truncated)
                        {
                            byte[] result = new byte[bytesWritten];
                            buffer.AsSpan(0, bytesWritten).CopyTo(result);
                            return result;
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
                        int bytesWritten = _binarySnapshotService.SerializeBitPackedDelta(buffer, objects, null, out bool truncated);
                        if (!truncated)
                        {
                            byte[] result = new byte[bytesWritten];
                            buffer.AsSpan(0, bytesWritten).CopyTo(result);
                            return result;
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                    bufferSize *= 2;
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
                    var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(bufferSize);
                    try
                    {
                        int bytesWritten = _binarySnapshotService.SerializeBitPackedDelta(buffer, objects, null, out bool truncated);
                        if (!truncated)
                        {
                            byte[] result = new byte[bytesWritten];
                            buffer.AsSpan(0, bytesWritten).CopyTo(result);
                            return result;
                        }
                    }
                    finally
                    {
                        System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                    }
                    bufferSize *= 2;
                }
            }
        }
    }
}
