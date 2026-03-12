using System.Runtime.CompilerServices;
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
        private readonly ConcurrentQueue<Cell> _activeCells = new();
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
            ulong res = 0;
            for (int i = 0; i < 32; i++)
            {
                res |= (ulong)(x & (1U << i)) << i;
                res |= (ulong)(y & (1U << i)) << (i + 1);
            }
            return res;
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
            var key = GetCellKey(obj.X, obj.Y);

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
                        AddInternal(obj, cell, (GetGridCoord(obj.X), GetGridCoord(obj.Y)));
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
                        AddInternal(obj, cell, (GetGridCoord(obj.X), GetGridCoord(obj.Y)));
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
                    AddInternal(obj, cell, (GetGridCoord(obj.X), GetGridCoord(obj.Y)));
                }
                finally
                {
                    if (lockTaken) cell.Lock.Exit(false);
                }
            }
        }

        private void AddInternal(IGameObject obj, Cell cell, (long X, long Y) key)
        {
            if (cell.Count == 0) _activeCells.Enqueue(cell);
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

            obj.NextInGridCell = null;
            obj.PrevInGridCell = null;
            obj.CurrentGridCellKey = null;
            cell.Count--;
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

        public void QueryBox(Box2l box, Action<IGameObject> callback)
        {
            long startGX = GetGridCoord(box.Left);
            long startGY = GetGridCoord(box.Bottom);
            long endGX = GetGridCoord(box.Right);
            long endGY = GetGridCoord(box.Top);

            for (long x = startGX; x <= endGX; x++)
            {
                for (long y = startGY; y <= endGY; y++)
                {
                    ulong key = GetMortonCode(x, y);
                    if (_grid.TryGetValue(key, out var cell))
                    {
                        // Double-checked pattern to avoid locking empty cells
                        if (Volatile.Read(ref cell.Head) == null) continue;

                        bool lockTaken = false;
                        try
                        {
                            cell.Lock.Enter(ref lockTaken);
                            var current = cell.Head;
                            while (current != null)
                            {
                                var next = current.NextInGridCell;
                                if (current.X >= box.Left && current.X <= box.Right && current.Y >= box.Bottom && current.Y <= box.Top)
                                {
                                    callback(current);
                                }
                                current = next;
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

        public void QueryBox<TState>(Box2l box, ref TState state, QueryCallback<TState> callback)
        {
            long startGX = GetGridCoord(box.Left);
            long startGY = GetGridCoord(box.Bottom);
            long endGX = GetGridCoord(box.Right);
            long endGY = GetGridCoord(box.Top);

            for (long x = startGX; x <= endGX; x++)
            {
                for (long y = startGY; y <= endGY; y++)
                {
                    ulong key = GetMortonCode(x, y);
                    if (_grid.TryGetValue(key, out var cell))
                    {
                        // Double-checked pattern to avoid locking empty cells
                        if (Volatile.Read(ref cell.Head) == null) continue;

                        bool lockTaken = false;
                        try
                        {
                            cell.Lock.Enter(ref lockTaken);
                            var current = cell.Head;
                            while (current != null)
                            {
                                var next = current.NextInGridCell;
                                if (current.X >= box.Left && current.X <= box.Right && current.Y >= box.Bottom && current.Y <= box.Top)
                                {
                                    callback(current, ref state);
                                }
                                current = next;
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

        public void CleanupEmptyCells()
        {
            foreach (var kvp in _grid)
            {
                var cell = kvp.Value;
                if (cell.Count == 0)
                {
                    bool lockTaken = false;
                    try
                    {
                        cell.Lock.Enter(ref lockTaken);
                        if (cell.Count == 0)
                        {
                            if (_grid.TryGetValue(kvp.Key, out var current) && current == cell)
                            {
                                if (_grid.TryRemove(kvp.Key, out var removed))
                                {
                                    removed.Clear();
                                    _cellPool.Push(removed);
                                }
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

        public void QueryBoxZ(Box2l box, long z, List<IGameObject> results)
        {
            long startGX = GetGridCoord(box.Left);
            long startGY = GetGridCoord(box.Bottom);
            long endGX = GetGridCoord(box.Right);
            long endGY = GetGridCoord(box.Top);

            for (long x = startGX; x <= endGX; x++)
            {
                for (long y = startGY; y <= endGY; y++)
                {
                    ulong key = GetMortonCode(x, y);
                    if (_grid.TryGetValue(key, out var cell))
                    {
                        if (Volatile.Read(ref cell.Head) == null) continue;

                        bool lockTaken = false;
                        try
                        {
                            cell.Lock.Enter(ref lockTaken);
                            var current = cell.Head;
                            while (current != null)
                            {
                                var next = current.NextInGridCell;
                                if (current.Z == z && current.X >= box.Left && current.X <= box.Right && current.Y >= box.Bottom && current.Y <= box.Top)
                                {
                                    results.Add(current);
                                }
                                current = next;
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

        public void GetObjectsInBox(Box2l box, List<IGameObject> results)
        {
            QueryBox(box, obj => results.Add(obj));
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
