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

            // Return detached COW arrays to the pool
            while (_replacedArrays.TryDequeue(out var array))
            {
                ArrayPool<IGameObject>.Shared.Return(array, true);
            }
        }

        private readonly ConcurrentDictionary<ulong, Cell> _grid = new();
        private readonly ConcurrentQueue<ulong> _emptyCellKeys = new();
        private readonly ConcurrentQueue<IGameObject[]> _replacedArrays = new();
        private readonly ConcurrentStack<Cell> _cellPool = new();
        private readonly int _cellSize;
        private readonly ILogger<SpatialGrid> _logger;
        private long _version;

        public long Version => Volatile.Read(ref _version);

        public SpatialGrid(ILogger<SpatialGrid> logger, int cellSize = 32)
        {
            _logger = logger;
            _cellSize = cellSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long GetGridCoord(long val)
        {
            return val >= 0 ? val / _cellSize : (val - _cellSize + 1) / _cellSize;
        }

        /// <summary>
        /// Computes a 64-bit Morton code for a 2D coordinate.
        /// This improves cache locality by mapping 2D proximity to 1D address proximity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong GetMortonCode(long x, long y)
        {
            // Map to positive range for bit interleaving
            uint ux = (uint)(x + 0x80000000);
            uint uy = (uint)(y + 0x80000000);

            return Interleave(ux, uy);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Interleave(uint x, uint y)
        {
            return InterleaveBits(x) | (InterleaveBits(y) << 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong InterleaveBits(uint x)
        {
            if (Bmi2.X64.IsSupported)
            {
                return Bmi2.X64.ParallelBitDeposit(x, 0x5555555555555555);
            }

            if (AdvSimd.Arm64.IsSupported)
            {
                // ARM64 bit interleaving optimization
                return InterleaveBitsArm64(x);
            }

            ulong val = x;
            val = (val | (val << 16)) & 0x0000FFFF0000FFFF;
            val = (val | (val << 8)) & 0x00FF00FF00FF00FF;
            val = (val | (val << 4)) & 0x0F0F0F0F0F0F0F0F;
            val = (val | (val << 2)) & 0x3333333333333333;
            val = (val | (val << 1)) & 0x5555555555555555;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong InterleaveBitsArm64(uint x)
        {
            // ARM64 doesn't have PDEP, but we can use AdvSimd for vector bit manipulation
            // if we were interleaving multiple values. For a single uint, we use a specialized sequence.
            ulong val = x;
            val = (val | (val << 16)) & 0x0000FFFF0000FFFF;
            val = (val | (val << 8)) & 0x00FF00FF00FF00FF;
            val = (val | (val << 4)) & 0x0F0F0F0F0F0F0F0F;
            val = (val | (val << 2)) & 0x3333333333333333;
            val = (val | (val << 1)) & 0x5555555555555555;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong GetCellKey(long x, long y)
        {
            return GetMortonCode(GetGridCoord(x), GetGridCoord(y));
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
            var x = obj.X;
            var y = obj.Y;
            var key = GetCellKey(x, y);

            if (obj.CurrentGridCellKey != null)
            {
                ulong oldKey = GetMortonCode(obj.CurrentGridCellKey.Value.X, obj.CurrentGridCellKey.Value.Y);
                if (oldKey == key) return;

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
                        AddInternal(obj, cell, (GetGridCoord(x), GetGridCoord(y)));
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
                        AddInternal(obj, cell, (GetGridCoord(x), GetGridCoord(y)));
                    }
                    finally
                    {
                        if (lock2) oldCell.Lock.Exit(false);
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
                    AddInternal(obj, cell, (GetGridCoord(x), GetGridCoord(y)));
                }
                finally
                {
                    if (lockTaken) cell.Lock.Exit(false);
                }
            }
        }

        private void AddInternal(IGameObject obj, Cell cell, (long X, long Y) key)
        {
            // Copy-On-Write to ensure lock-free reads during iteration
            int newCount = cell.Count + 1;
            var oldArray = cell.Objects;

            // If the current array has enough capacity, we can still COW by renting a new one of the SAME size
            // This ensures active enumerators aren't affected while minimizing overhead if we're well within capacity
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
                // Enqueue old array for deferred return to avoid race with active enumerators
                _replacedArrays.Enqueue(oldArray);
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
            ulong key = GetMortonCode(obj.CurrentGridCellKey.Value.X, obj.CurrentGridCellKey.Value.Y);
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
                _replacedArrays.Enqueue(oldArray);
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
                _emptyCellKeys.Enqueue(GetMortonCode(oldKey.Value.X, oldKey.Value.Y));
            }
        }

        public void Update(IGameObject obj, long oldX, long oldY)
        {
            Add(obj);
        }

        public List<IGameObject> GetObjectsInBox(Box2l box)
        {
            var results = new List<IGameObject>();
            GetObjectsInBox(box, results);
            return results;
        }

        public delegate void QueryCallback<TState>(IGameObject obj, ref TState state);

        public struct BoxEnumerator : IEnumerator<IGameObject>
        {
            private readonly SpatialGrid _grid;
            private readonly Box2l _box;
            private readonly long _endGX;
            private readonly long _endGY;
            private long _currentGX;
            private long _currentGY;
            private int _currentIndexInCell;
            private Cell? _currentCell;
            private IGameObject? _current;

            public BoxEnumerator(SpatialGrid grid, Box2l box)
            {
                _grid = grid;
                _box = box;
                _currentGX = grid.GetGridCoord(box.Left);
                _currentGY = grid.GetGridCoord(box.Bottom);
                _endGX = grid.GetGridCoord(box.Right);
                _endGY = grid.GetGridCoord(box.Top);
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
                            // Re-verify bounds as object might have moved since it was added to this cell
                            // but BEFORE COW update happens (though our COW is strict).
                            // Actually, it's more to handle objects overlapping cell boundaries or being partially in box.
                            if (_current != null && _current.X >= _box.Left && _current.X <= _box.Right &&
                                _current.Y >= _box.Bottom && _current.Y <= _box.Top)
                            {
                                return true;
                            }
                            continue;
                        }
                    }

                    if (_currentGY > _endGY)
                    {
                        _currentGY = _grid.GetGridCoord(_box.Bottom);
                        _currentGX++;
                    }

                    if (_currentGX > _endGX) return false;

                    ulong key = GetMortonCode(_currentGX, _currentGY);
                    _currentGY++;

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

        public BoxEnumerator GetEnumerator(Box2l box) => new BoxEnumerator(this, box);

        public void QueryBox(Box2l box, Action<IGameObject> callback)
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

        public void QueryBox<TState>(Box2l box, ref TState state, QueryCallback<TState> callback)
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
            while (_emptyCellKeys.TryDequeue(out var key))
            {
                if (_grid.TryGetValue(key, out var cell))
                {
                    if (cell.Count == 0)
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
                                    if (array.Length > 0) _replacedArrays.Enqueue(array);
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

        public void QueryBoxZ(Box2l box, long z, List<IGameObject> results)
        {
            var enumerator = new BoxEnumerator(this, box);
            try
            {
                while (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    if (current.Z == z)
                    {
                        results.Add(current);
                    }
                }
            }
            finally
            {
                enumerator.Dispose();
            }
        }

        public void GetObjectsInBox(Box2l box, List<IGameObject> results)
        {
            results.Clear();
            GetObjectsInBox(box, (IList<IGameObject>)results);
        }

        /// <summary>
        /// Retrieves all objects in the given box and adds them to the results list.
        /// This overload allows the caller to provide a pre-allocated list to avoid heap churn.
        /// </summary>
        public void GetObjectsInBox(Box2l box, IList<IGameObject> results)
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

            while (_replacedArrays.TryDequeue(out var array))
            {
                ArrayPool<IGameObject>.Shared.Return(array, true);
            }

            GC.SuppressFinalize(this);
        }

        ~SpatialGrid()
        {
            Dispose();
        }
    }
