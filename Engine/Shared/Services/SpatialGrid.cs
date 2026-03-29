using System.Runtime.CompilerServices;
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
            public int Count;
            public SpinLock Lock = new(false);
            public long Version;

            public void Reset()
            {
                // Note: We don't return to pool here anymore, parent grid handles it via _replacedArrays
                Objects = Array.Empty<IGameObject>();
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

        private readonly ConcurrentDictionary<ulong, Cell> _grid = new();
        private readonly ConcurrentQueue<ulong> _emptyCellKeys = new();
        private readonly ConcurrentQueue<(long ReleaseTimestamp, IGameObject[] Array)> _replacedArrays = new();
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
            // Copy-On-Write optimization: only COW if we actually need to expand or if we want to ensure isolation.
            // For now, we maintain strict COW for thread-safety during parallel queries.
            int newCount = cell.Count + 1;
            var oldArray = cell.Objects;

            int targetSize = oldArray.Length;
            if (newCount > targetSize)
            {
                targetSize = targetSize == 0 ? 4 : targetSize * 2;
            }

            var newArray = ArrayPool<IGameObject>.Shared.Rent(targetSize);
            if (cell.Count > 0)
            {
                Array.Copy(oldArray, newArray, cell.Count);
            }

            if (oldArray.Length > 0)
            {
                EnqueueForRelease(oldArray);
            }

            obj.SpatialGridIndex = cell.Count;
            newArray[cell.Count] = obj;
            cell.Objects = newArray;
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

            // We still COW even on remove to maintain thread-safety for enumerators
            var newArray = ArrayPool<IGameObject>.Shared.Rent(oldArray.Length);
            // Ensure the rented array is clean of stale references before use
            Array.Clear(newArray, 0, newArray.Length);

            if (lastIndex > 0)
            {
                Array.Copy(oldArray, newArray, cell.Count);
                if (index < lastIndex)
                {
                    var lastObj = newArray[lastIndex];
                    newArray[index] = lastObj;
                    lastObj.SpatialGridIndex = index;
                }
                newArray[lastIndex] = null!;
            }

            if (oldArray.Length > 0)
            {
                EnqueueForRelease(oldArray);
            }

            cell.Objects = newArray;
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
                        _currentIndexInCell++;
                        // Lock-free read of COW array
                        var objs = _currentCell.Objects;
                        int count = _currentCell.Count;
                        if (_currentIndexInCell < count && _currentIndexInCell < objs.Length)
                        {
                            _current = objs[_currentIndexInCell];
                            if (_current != null && _current.X >= _box.Left && _current.X <= _box.Right &&
                                _current.Y >= _box.Bottom && _current.Y <= _box.Top &&
                                _current.Z >= _box.Back && _current.Z <= _box.Front)
                            {
                                return true;
                            }
                            continue;
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
                                    var array = removed.Objects;
                                    if (array.Length > 0) EnqueueForRelease(array);
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
                var array = cell.Objects;
                if (array.Length > 0) ArrayPool<IGameObject>.Shared.Return(array, true);
            }
            _grid.Clear();

            while (_cellPool.TryPop(out var cell))
            {
                var array = cell.Objects;
                if (array.Length > 0) ArrayPool<IGameObject>.Shared.Return(array, true);
            }

            while (_replacedArrays.TryDequeue(out var entry))
            {
                ArrayPool<IGameObject>.Shared.Return(entry.Array, true);
            }

            GC.SuppressFinalize(this);
        }

        ~SpatialGrid()
        {
            Dispose();
        }
    }
