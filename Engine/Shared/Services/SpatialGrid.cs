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

        private readonly ConcurrentDictionary<(long X, long Y), Cell> _grid = new();
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (long X, long Y) GetCellKey(long x, long y)
        {
            return (GetGridCoord(x), GetGridCoord(y));
        }

        private Cell GetOrCreateCell((long X, long Y) key)
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
                (long X, long Y) oldKey = obj.CurrentGridCellKey.Value;
                if (oldKey == key) return;

                var oldCell = GetOrCreateCell(oldKey);
                var cell = GetOrCreateCell(key);

                // Consistent lock ordering to avoid deadlocks
                if (CompareKeys(oldKey, key) < 0)
                {
                    bool lock1 = false, lock2 = false;
                    try
                    {
                        oldCell.Lock.Enter(ref lock1);
                        cell.Lock.Enter(ref lock2);
                        RemoveInternal(obj, oldCell);
                        AddInternal(obj, cell, key);
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
                        AddInternal(obj, cell, key);
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
                    AddInternal(obj, cell, key);
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
            if (_grid.TryGetValue(obj.CurrentGridCellKey.Value, out var cell))
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
                    if (_grid.TryGetValue((x, y), out var cell))
                    {
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
                    if (_grid.TryGetValue((x, y), out var cell))
                    {
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

        public void GetObjectsInBox(Box2l box, List<IGameObject> results)
        {
            QueryBox(box, obj => results.Add(obj));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CompareKeys((long X, long Y) a, (long X, long Y) b)
        {
            int res = a.X.CompareTo(b.X);
            if (res != 0) return res;
            return a.Y.CompareTo(b.Y);
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
