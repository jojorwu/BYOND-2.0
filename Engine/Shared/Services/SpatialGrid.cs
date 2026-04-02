using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;
using System;
using System.Collections.Generic;
using System.Threading;
using Robust.Shared.Maths;
using System.Collections.Concurrent;
using System.Buffers;
using Shared.Interfaces;
using Microsoft.Extensions.Logging;
using Shared.Services;

namespace Shared;
    public class SpatialGrid : EngineService, IDisposable, IShrinkable
    {
        private class Cell
        {
            public volatile IGameObject[] Objects = Array.Empty<IGameObject>();
            public volatile long[] Xs = Array.Empty<long>();
            public volatile long[] Ys = Array.Empty<long>();
            public volatile long[] Zs = Array.Empty<long>();
            public int Count;
            public SpinLock Lock = new(false);
            public long Version;

            public void Reset()
            {
                // Note: We don't return to pool here anymore, parent grid handles it via _replacedArrays
                Objects = Array.Empty<IGameObject>();
                Xs = Array.Empty<long>();
                Ys = Array.Empty<long>();
                Zs = Array.Empty<long>();
                Count = 0;
                Version = 0;
            }
        }

        public void Shrink()
        {
            CleanupEmptyCells();

            // Return detached COW arrays to the pool after grace period
            long now = _timeProvider.GetTimestamp();
            while (_replacedArrays.TryPeek(out var entry) && now >= entry.ReleaseTimestamp)
            {
                if (_replacedArrays.TryDequeue(out entry))
                {
                    ArrayPool<IGameObject>.Shared.Return(entry.Array, true);
                }
            }

            while (_replacedCoordArrays.TryPeek(out var entry) && now >= entry.ReleaseTimestamp)
            {
                if (_replacedCoordArrays.TryDequeue(out entry))
                {
                    ArrayPool<long>.Shared.Return(entry.Array, true);
                }
            }

            _diagnosticBus.Publish("SpatialGrid", "Grid status update", (CellCount: _grid.Count, ReplacedCount: _replacedArrays.Count, Queries: Interlocked.Read(ref _totalQueries), Moved: Interlocked.Read(ref _objectsMoved)), (m, state) =>
            {
                m.Add("CellCount", state.CellCount);
                m.Add("ReplacedArraysCount", state.ReplacedCount);
                m.Add("TotalQueries", state.Queries);
                m.Add("ObjectsMoved", state.Moved);
            });
        }

        private void EnqueueForRelease(IGameObject[] array)
        {
            if (array.Length == 0) return;
            // Grace period of 1 second to ensure all active enumerators are finished
            long releaseAt = _timeProvider.GetTimestamp() + _timeProvider.TimestampFrequency;
            _replacedArrays.Enqueue((releaseAt, array));
        }

        private void EnqueueForRelease(long[] array)
        {
            if (array.Length == 0) return;
            long releaseAt = _timeProvider.GetTimestamp() + _timeProvider.TimestampFrequency;
            _replacedCoordArrays.Enqueue((releaseAt, array));
        }

        private readonly ConcurrentDictionary<ulong, Cell> _grid = new();
        private readonly ConcurrentQueue<ulong> _emptyCellKeys = new();
        private readonly ConcurrentQueue<(long ReleaseTimestamp, IGameObject[] Array)> _replacedArrays = new();
        private readonly ConcurrentQueue<(long ReleaseTimestamp, long[] Array)> _replacedCoordArrays = new();
        private readonly ConcurrentStack<Cell> _cellPool = new();
        private readonly TimeProvider _timeProvider;
        private readonly int _cellSize;
        private readonly ILogger<SpatialGrid> _logger;
        private readonly IDiagnosticBus _diagnosticBus;
        private long _version;
        private long _totalQueries;
        private long _objectsMoved;

        public long Version => Volatile.Read(ref _version);

        public SpatialGrid(ILogger<SpatialGrid> logger, TimeProvider timeProvider, IDiagnosticBus diagnosticBus, int cellSize = 32)
        {
            _logger = logger;
            _timeProvider = timeProvider;
            _diagnosticBus = diagnosticBus;
            _cellSize = cellSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long GetGridCoord(long val)
        {
            return val >= 0 ? val / _cellSize : (val - _cellSize + 1) / _cellSize;
        }

        /// <summary>
        /// Computes a 64-bit Morton code for a 3D coordinate (21 bits per axis).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong GetMortonCode3D(long x, long y, long z)
        {
            return Interleave3((uint)(x + 0x100000), (uint)(y + 0x100000), (uint)(z + 0x100000));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Interleave3(uint x, uint y, uint z)
        {
            return SpreadBits3(x) | (SpreadBits3(y) << 1) | (SpreadBits3(z) << 2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong SpreadBits3(uint x)
        {
            if (Bmi2.X64.IsSupported)
            {
                // Optimization: Use hardware-accelerated Parallel Bit Deposit if supported (x64)
                // This spreads the 21 bits of 'x' across the 64-bit result, with 2 zero bits between each set bit.
                return Bmi2.X64.ParallelBitDeposit(x, 0x9249249249249249UL);
            }

            ulong val = x & 0x1FFFFF;
            val = (val | (val << 32)) & 0x1F00000000FFFFUL;
            val = (val | (val << 16)) & 0x1F0000FF0000FFUL;
            val = (val | (val << 8)) & 0x100F00F00F00F00FUL;
            val = (val | (val << 4)) & 0x10C30C30C30C30C3UL;
            val = (val | (val << 2)) & 0x1249249249249249UL;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong GetCellKey(long x, long y, long z)
        {
            return GetMortonCode3D(GetGridCoord(x), GetGridCoord(y), GetGridCoord(z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong GetCellKeyFromVector(Vector3l vec, SpatialGrid grid)
        {
            return grid.GetCellKey(vec.X, vec.Y, vec.Z);
        }

        private Cell GetOrCreateCell(ulong key)
        {
            return _grid.GetOrAdd(key, _ => {
                if (!_cellPool.TryPop(out var pooled)) pooled = new Cell();
                return pooled;
            });
        }

        public void Add(IGameObject obj)
        {
            var pos = obj.Position;
            var key = GetCellKey(pos.X, pos.Y, pos.Z);

            if (obj.CurrentGridCellKey != null)
            {
                ulong oldKey = GetMortonCode3D(obj.CurrentGridCellKey.Value.X, obj.CurrentGridCellKey.Value.Y, obj.CurrentGridCellKey.Value.Z);
                if (oldKey == key) return;

                Interlocked.Increment(ref _objectsMoved);

                var oldCell = GetOrCreateCell(oldKey);
                var cell = GetOrCreateCell(key);

                // Consistent lock ordering to avoid deadlocks (Morton codes are stable unique ulongs)
                if (oldKey < key)
                {
                    bool lock1 = false, lock2 = false;
                    try
                    {
                        oldCell.Lock.Enter(ref lock1);
                        cell.Lock.Enter(ref lock2);
                        RemoveInternal(obj, oldCell);
                        AddInternal(obj, cell, new Vector3l(GetGridCoord(pos.X), GetGridCoord(pos.Y), GetGridCoord(pos.Z)));
                    }
                    finally
                    {
                        if (lock2) cell.Lock.Exit(false);
                        if (lock1) oldCell.Lock.Exit(false);
                    }
                }
                else
                {
                    bool lock1 = false, lock2 = false;
                    try
                    {
                        cell.Lock.Enter(ref lock1);
                        oldCell.Lock.Enter(ref lock2);
                        RemoveInternal(obj, oldCell);
                        AddInternal(obj, cell, new Vector3l(GetGridCoord(pos.X), GetGridCoord(pos.Y), GetGridCoord(pos.Z)));
                    }
                    finally
                    {
                        if (lock2) cell.Lock.Exit(false);
                        if (lock1) cell.Lock.Exit(false);
                    }
                }
            }
            else
            {
                var cell = GetOrCreateCell(key);
                bool lockTaken = false;
                try
                {
                    cell.Lock.Enter(ref lockTaken);
                    AddInternal(obj, cell, new Vector3l(GetGridCoord(pos.X), GetGridCoord(pos.Y), GetGridCoord(pos.Z)));
                }
                finally
                {
                    if (lockTaken) cell.Lock.Exit(false);
                }
            }
        }

        private void AddInternal(IGameObject obj, Cell cell, Vector3l key)
        {
            int newCount = cell.Count + 1;
            var oldArray = cell.Objects;
            var oldX = cell.Xs;
            var oldY = cell.Ys;
            var oldZ = cell.Zs;

            int targetSize = oldArray.Length;
            if (newCount > targetSize)
            {
                targetSize = targetSize == 0 ? 4 : targetSize * 2;
            }

            var newArray = ArrayPool<IGameObject>.Shared.Rent(targetSize);
            var newX = ArrayPool<long>.Shared.Rent(targetSize);
            var newY = ArrayPool<long>.Shared.Rent(targetSize);
            var newZ = ArrayPool<long>.Shared.Rent(targetSize);

            if (cell.Count > 0)
            {
                Array.Copy(oldArray, newArray, cell.Count);
                Array.Copy(oldX, newX, cell.Count);
                Array.Copy(oldY, newY, cell.Count);
                Array.Copy(oldZ, newZ, cell.Count);
            }

            if (oldArray.Length > 0)
            {
                EnqueueForRelease(oldArray);
                EnqueueForRelease(oldX);
                EnqueueForRelease(oldY);
                EnqueueForRelease(oldZ);
            }

            obj.SpatialGridIndex = cell.Count;
            newArray[cell.Count] = obj;
            newX[cell.Count] = obj.X;
            newY[cell.Count] = obj.Y;
            newZ[cell.Count] = obj.Z;

            cell.Objects = newArray;
            cell.Xs = newX;
            cell.Ys = newY;
            cell.Zs = newZ;

            cell.Count = newCount;
            cell.Version++;
            Interlocked.Increment(ref _version);
            obj.CurrentGridCellKey = key;
        }

        public void Remove(IGameObject obj)
        {
            if (obj.CurrentGridCellKey == null) return;
            var pos = obj.CurrentGridCellKey.Value;
            ulong key = GetMortonCode3D(pos.X, pos.Y, pos.Z);
            if (_grid.TryGetValue(key, out var cell))
            {
                bool lockTaken = false;
                try
                {
                    cell.Lock.Enter(ref lockTaken);
                    RemoveInternal(obj, cell);
                }
                finally
                {
                    if (lockTaken) cell.Lock.Exit(false);
                }
            }
        }

        private void RemoveInternal(IGameObject obj, Cell cell)
        {
            int index = obj.SpatialGridIndex;
            if (index == -1) return;

            int lastIndex = cell.Count - 1;
            var oldArray = cell.Objects;
            var oldX = cell.Xs;
            var oldY = cell.Ys;
            var oldZ = cell.Zs;

            var newArray = ArrayPool<IGameObject>.Shared.Rent(oldArray.Length);
            var newX = ArrayPool<long>.Shared.Rent(oldX.Length);
            var newY = ArrayPool<long>.Shared.Rent(oldY.Length);
            var newZ = ArrayPool<long>.Shared.Rent(oldZ.Length);

            Array.Clear(newArray, 0, newArray.Length);

            if (lastIndex > 0)
            {
                Array.Copy(oldArray, newArray, cell.Count);
                Array.Copy(oldX, newX, cell.Count);
                Array.Copy(oldY, newY, cell.Count);
                Array.Copy(oldZ, newZ, cell.Count);

                if (index < lastIndex)
                {
                    var lastObj = newArray[lastIndex];
                    newArray[index] = lastObj;
                    newX[index] = newX[lastIndex];
                    newY[index] = newY[lastIndex];
                    newZ[index] = newZ[lastIndex];
                    lastObj.SpatialGridIndex = index;
                }
                newArray[lastIndex] = null!;
            }

            if (oldArray.Length > 0)
            {
                EnqueueForRelease(oldArray);
                EnqueueForRelease(oldX);
                EnqueueForRelease(oldY);
                EnqueueForRelease(oldZ);
            }

            cell.Objects = newArray;
            cell.Xs = newX;
            cell.Ys = newY;
            cell.Zs = newZ;

            cell.Count--;
            cell.Version++;
            Interlocked.Increment(ref _version);
            obj.SpatialGridIndex = -1;
            var oldKey = obj.CurrentGridCellKey;
            obj.CurrentGridCellKey = null;

            if (cell.Count == 0 && oldKey != null)
            {
                _emptyCellKeys.Enqueue(GetMortonCode3D(oldKey.Value.X, oldKey.Value.Y, oldKey.Value.Z));
            }
        }

        public void Update(IGameObject obj, long oldX, long oldY)
        {
            Add(obj);
        }

        public List<IGameObject> GetObjectsInBox(Box3l box)
        {
            var results = new List<IGameObject>();
            GetObjectsInBox(box, results);
            return results;
        }

        public delegate void QueryCallback<TState>(IGameObject obj, ref TState state);

        public struct BoxEnumerator : IEnumerator<IGameObject>
        {
            private readonly SpatialGrid _grid;
            private readonly Box3l _box;
            private readonly long _endGX;
            private readonly long _endGY;
            private readonly long _endGZ;
            private long _currentGX;
            private long _currentGY;
            private long _currentGZ;
            private int _currentIndexInCell;
            private Cell? _currentCell;
            private IGameObject? _current;

            public BoxEnumerator(SpatialGrid grid, Box3l box)
            {
                _grid = grid;
                Interlocked.Increment(ref _grid._totalQueries);
                _box = box;
                _currentGX = grid.GetGridCoord(box.Left);
                _currentGY = grid.GetGridCoord(box.Bottom);
                _currentGZ = grid.GetGridCoord(box.Back);
                _endGX = grid.GetGridCoord(box.Right);
                _endGY = grid.GetGridCoord(box.Top);
                _endGZ = grid.GetGridCoord(box.Front);
                _currentIndexInCell = -1;
                _currentCell = null;
                _current = null;
            }

            public bool MoveNext()
            {
                while (true)
                {
                    if (_currentCell != null)
                    {
                        var objs = _currentCell.Objects;
                        var xs = _currentCell.Xs;
                        var ys = _currentCell.Ys;
                        var zs = _currentCell.Zs;
                        int count = _currentCell.Count;

                        while (++_currentIndexInCell < count)
                        {
                            // SIMD-Accelerated filtering (AVX2)
                            if (Vector256.IsHardwareAccelerated && count - _currentIndexInCell >= 4)
                            {
                                var vx = Vector256.Create(xs, _currentIndexInCell);
                                var vy = Vector256.Create(ys, _currentIndexInCell);
                                var vz = Vector256.Create(zs, _currentIndexInCell);

                                var vLeft = Vector256.Create(_box.Left);
                                var vRight = Vector256.Create(_box.Right);
                                var vBottom = Vector256.Create(_box.Bottom);
                                var vTop = Vector256.Create(_box.Top);
                                var vBack = Vector256.Create(_box.Back);
                                var vFront = Vector256.Create(_box.Front);

                                var mask = Vector256.GreaterThanOrEqual(vx, vLeft) & Vector256.LessThanOrEqual(vx, vRight) &
                                           Vector256.GreaterThanOrEqual(vy, vBottom) & Vector256.LessThanOrEqual(vy, vTop) &
                                           Vector256.GreaterThanOrEqual(vz, vBack) & Vector256.LessThanOrEqual(vz, vFront);

                                uint moveMask = (uint)Vector256.ExtractMostSignificantBits(mask);
                                if (moveMask != 0)
                                {
                                    int bit = System.Numerics.BitOperations.TrailingZeroCount(moveMask);
                                    _currentIndexInCell += bit;
                                    _current = objs[_currentIndexInCell];
                                    return true;
                                }
                                _currentIndexInCell += 3;
                                continue;
                            }
                            // SIMD-Accelerated filtering (NEON)
                            else if (AdvSimd.IsSupported && count - _currentIndexInCell >= 2)
                            {
                                var vx = Vector128.Create(xs[_currentIndexInCell], xs[_currentIndexInCell + 1]);
                                var vy = Vector128.Create(ys[_currentIndexInCell], ys[_currentIndexInCell + 1]);
                                var vz = Vector128.Create(zs[_currentIndexInCell], zs[_currentIndexInCell + 1]);

                                var vLeft = Vector128.Create(_box.Left);
                                var vRight = Vector128.Create(_box.Right);
                                var vBottom = Vector128.Create(_box.Bottom);
                                var vTop = Vector128.Create(_box.Top);
                                var vBack = Vector128.Create(_box.Back);
                                var vFront = Vector128.Create(_box.Front);

                                var mask = Vector128.GreaterThanOrEqual(vx, vLeft) & Vector128.LessThanOrEqual(vx, vRight) &
                                           Vector128.GreaterThanOrEqual(vy, vBottom) & Vector128.LessThanOrEqual(vy, vTop) &
                                           Vector128.GreaterThanOrEqual(vz, vBack) & Vector128.LessThanOrEqual(vz, vFront);

                                if (mask.GetElement(0) != 0)
                                {
                                    _current = objs[_currentIndexInCell];
                                    return true;
                                }
                                if (mask.GetElement(1) != 0)
                                {
                                    _currentIndexInCell++;
                                    _current = objs[_currentIndexInCell];
                                    return true;
                                }
                                _currentIndexInCell++;
                                continue;
                            }

                            if (xs[_currentIndexInCell] >= _box.Left && xs[_currentIndexInCell] <= _box.Right &&
                                ys[_currentIndexInCell] >= _box.Bottom && ys[_currentIndexInCell] <= _box.Top &&
                                zs[_currentIndexInCell] >= _box.Back && zs[_currentIndexInCell] <= _box.Front)
                            {
                                _current = objs[_currentIndexInCell];
                                return true;
                            }
                        }
                    }

                    if (_currentGZ > _endGZ)
                    {
                        _currentGZ = _grid.GetGridCoord(_box.Back);
                        _currentGY++;
                    }

                    if (_currentGY > _endGY)
                    {
                        _currentGY = _grid.GetGridCoord(_box.Bottom);
                        _currentGX++;
                    }

                    if (_currentGX > _endGX) return false;

                    ulong key = GetMortonCode3D(_currentGX, _currentGY, _currentGZ);
                    _currentGZ++;

                    if (_grid._grid.TryGetValue(key, out _currentCell))
                    {
                        if (Volatile.Read(ref _currentCell.Count) > 0)
                        {
                            _currentIndexInCell = -1;
                        }
                        else
                        {
                            _currentCell = null;
                        }
                    }
                }
            }

            public IGameObject Current => _current!;
            object System.Collections.IEnumerator.Current => Current;

            public void Reset() => throw new NotSupportedException();

            public void Dispose() { }

            public BoxEnumerator GetEnumerator() => this;
        }

        public BoxEnumerator GetEnumerator(Box3l box) => new BoxEnumerator(this, box);

        public void QueryBox(Box3l box, Action<IGameObject> callback)
        {
            var enumerator = new BoxEnumerator(this, box);
            try
            {
                while (enumerator.MoveNext())
                {
                    callback(enumerator.Current);
                }
            }
            finally
            {
                enumerator.Dispose();
            }
        }

        public void QueryBox<TState>(Box3l box, ref TState state, QueryCallback<TState> callback)
        {
            var enumerator = new BoxEnumerator(this, box);
            try
            {
                while (enumerator.MoveNext())
                {
                    callback(enumerator.Current, ref state);
                }
            }
            finally
            {
                enumerator.Dispose();
            }
        }

        public void CleanupEmptyCells()
        {
            // Optimization: Process empty cells in batches to reduce lock contention
            int processed = 0;
            while (processed < 1024 && _emptyCellKeys.TryDequeue(out var key))
            {
                processed++;
                if (_grid.TryGetValue(key, out var cell))
                {
                    if (Volatile.Read(ref cell.Count) == 0)
                    {
                        bool lockTaken = false;
                        try
                        {
                            cell.Lock.Enter(ref lockTaken);
                            if (cell.Count == 0)
                            {
                                if (_grid.TryRemove(key, out var removed))
                                {
                                    if (removed.Objects.Length > 0) EnqueueForRelease(removed.Objects);
                                    if (removed.Xs.Length > 0) EnqueueForRelease(removed.Xs);
                                    if (removed.Ys.Length > 0) EnqueueForRelease(removed.Ys);
                                    if (removed.Zs.Length > 0) EnqueueForRelease(removed.Zs);

                                    removed.Reset();
                                    _cellPool.Push(removed);
                                }
                            }
                        }
                        finally
                        {
                            if (lockTaken) cell.Lock.Exit(false);
                        }
                    }
                }
            }
        }

        public void GetObjectsInBox(Box3l box, List<IGameObject> results)
        {
            results.Clear();
            GetObjectsInBox(box, (IList<IGameObject>)results);
        }

        /// <summary>
        /// Retrieves all objects in the given box and adds them to the results list.
        /// This overload allows the caller to provide a pre-allocated list to avoid heap churn.
        /// </summary>
        public void GetObjectsInBox(Box3l box, IList<IGameObject> results)
        {
            var enumerator = new BoxEnumerator(this, box);
            try
            {
                while (enumerator.MoveNext())
                {
                    results.Add(enumerator.Current);
                }
            }
            finally
            {
                enumerator.Dispose();
            }
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            Dispose();
            return Task.CompletedTask;
        }

        public override Dictionary<string, object> GetDiagnosticInfo()
        {
            var info = base.GetDiagnosticInfo();
            info["CellCount"] = _grid.Count;
            info["PooledCells"] = _cellPool.Count;
            info["PendingArrays"] = _replacedArrays.Count;
            info["TotalQueries"] = Interlocked.Read(ref _totalQueries);
            info["ObjectsMoved"] = Interlocked.Read(ref _objectsMoved);
            return info;
        }

        public void Dispose()
        {
            foreach (var cell in _grid.Values)
            {
                if (cell.Objects.Length > 0) ArrayPool<IGameObject>.Shared.Return(cell.Objects, true);
                if (cell.Xs.Length > 0) ArrayPool<long>.Shared.Return(cell.Xs, true);
                if (cell.Ys.Length > 0) ArrayPool<long>.Shared.Return(cell.Ys, true);
                if (cell.Zs.Length > 0) ArrayPool<long>.Shared.Return(cell.Zs, true);
            }
            _grid.Clear();

            while (_cellPool.TryPop(out var cell))
            {
                if (cell.Objects.Length > 0) ArrayPool<IGameObject>.Shared.Return(cell.Objects, true);
                if (cell.Xs.Length > 0) ArrayPool<long>.Shared.Return(cell.Xs, true);
                if (cell.Ys.Length > 0) ArrayPool<long>.Shared.Return(cell.Ys, true);
                if (cell.Zs.Length > 0) ArrayPool<long>.Shared.Return(cell.Zs, true);
            }

            while (_replacedArrays.TryDequeue(out var entry))
            {
                ArrayPool<IGameObject>.Shared.Return(entry.Array, true);
            }
            while (_replacedCoordArrays.TryDequeue(out var entry))
            {
                ArrayPool<long>.Shared.Return(entry.Array, true);
            }

            GC.SuppressFinalize(this);
        }

        ~SpatialGrid()
        {
            Dispose();
        }
    }
