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

namespace Shared;
    public class SpatialGrid : IDisposable, IShrinkable
    {
        private class Cell : IDisposable
        {
            public IGameObject? Head;
            public int Count;
            public SpinLock Lock = new(false);

            public void Clear()
            {
                var current = Head;
                while (current != null)
                {
                    var next = current.NextInGridCell;
                    current.NextInGridCell = null;
                    current.PrevInGridCell = null;
                    current.CurrentGridCellKey = null;
                    current = next;
                }
                Head = null;
                Count = 0;
            }

            public void Dispose()
            {
            }
        }

        public void Shrink()
        {
            CleanupEmptyCells();
        }

        private readonly ConcurrentDictionary<ulong, Cell> _grid = new();
        private readonly ConcurrentQueue<ulong> _emptyCellKeys = new();
        private readonly ConcurrentStack<Cell> _cellPool = new();
        private readonly int _cellSize;
        private readonly ILogger<SpatialGrid> _logger;

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
                // Optimization for ARM64: Utilize bit manipulation for coordinate interleaving
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
            obj.NextInGridCell = cell.Head;
            obj.PrevInGridCell = null;
            if (cell.Head != null) cell.Head.PrevInGridCell = obj;
            cell.Head = obj;
            cell.Count++;
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
            if (cell.Head == obj) cell.Head = obj.NextInGridCell;
            if (obj.PrevInGridCell != null) obj.PrevInGridCell.NextInGridCell = obj.NextInGridCell;
            if (obj.NextInGridCell != null) obj.NextInGridCell.PrevInGridCell = obj.PrevInGridCell;

            var oldKey = obj.CurrentGridCellKey;
            obj.NextInGridCell = null;
            obj.PrevInGridCell = null;
            obj.CurrentGridCellKey = null;
            cell.Count--;

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

        public struct BoxEnumerator
        {
            private readonly SpatialGrid _grid;
            private readonly Box2l _box;
            private readonly long _endGX;
            private readonly long _endGY;
            private long _currentGX;
            private long _currentGY;
            private IGameObject? _currentInCell;
            private Cell? _currentCell;
            private bool _lockTaken;

            public BoxEnumerator(SpatialGrid grid, Box2l box)
            {
                _grid = grid;
                _box = box;
                _currentGX = grid.GetGridCoord(box.Left);
                _currentGY = grid.GetGridCoord(box.Bottom);
                _endGX = grid.GetGridCoord(box.Right);
                _endGY = grid.GetGridCoord(box.Top);
                _currentInCell = null;
                _currentCell = null;
                _lockTaken = false;
            }

            public bool MoveNext()
            {
                while (true)
                {
                    if (_currentInCell != null)
                    {
                        _currentInCell = _currentInCell.NextInGridCell;
                    }

                    while (_currentInCell == null)
                    {
                        if (_lockTaken && _currentCell != null)
                        {
                            _currentCell.Lock.Exit(false);
                            _lockTaken = false;
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
                            if (Volatile.Read(ref _currentCell.Head) != null)
                            {
                                _currentCell.Lock.Enter(ref _lockTaken);
                                _currentInCell = _currentCell.Head;
                            }
                        }
                    }

                    if (_currentInCell.X >= _box.Left && _currentInCell.X <= _box.Right &&
                        _currentInCell.Y >= _box.Bottom && _currentInCell.Y <= _box.Top)
                    {
                        return true;
                    }
                }
            }

            public IGameObject Current => _currentInCell!;

            public void Dispose()
            {
                if (_lockTaken && _currentCell != null)
                {
                    _currentCell.Lock.Exit(false);
                    _lockTaken = false;
                }
            }

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
                                    removed.Clear();
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

        public void Dispose()
        {
            _grid.Clear();
            _cellPool.Clear();
            GC.SuppressFinalize(this);
        }

        ~SpatialGrid()
        {
            Dispose();
        }
    }
