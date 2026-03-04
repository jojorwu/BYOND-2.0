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
            public readonly ReaderWriterLockSlim Lock = new(LockRecursionPolicy.NoRecursion);

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
            }

            public void Dispose()
            {
                Lock.Dispose();
            }
        }

        public void Shrink()
        {
            CleanupEmptyCells();
        }

        private readonly ConcurrentDictionary<(long X, long Y), Cell> _grid = new();
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
            while (true)
            {
                var cell = _grid.GetOrAdd(key, _ => {
                    if (!_cellPool.TryPop(out var pooled)) pooled = new Cell();
                    return pooled;
                });
                cell.Lock.EnterReadLock();
                try
                {
                    if (_grid.TryGetValue(key, out var current) && current == cell)
                    {
                        return cell;
                    }
                }
                finally
                {
                    cell.Lock.ExitReadLock();
                }
            }
        }

        public void Add(IGameObject obj)
        {
            var key = GetCellKey(obj.X, obj.Y);
            var cell = GetOrCreateCell(key);

            if (obj.CurrentGridCellKey != null)
            {
                (long X, long Y) oldKey = obj.CurrentGridCellKey.Value;
                if (oldKey == key) return;

                var oldCell = GetOrCreateCell(oldKey);
                // Consistent lock ordering to avoid deadlocks
                if (CompareKeys(oldKey, key) < 0)
                {
                    oldCell.Lock.EnterWriteLock();
                    try
                    {
                        cell.Lock.EnterWriteLock();
                        try
                        {
                            RemoveInternal(obj);
                            AddInternal(obj, cell, key);
                        }
                        finally
                        {
                            cell.Lock.ExitWriteLock();
                        }
                    }
                    finally
                    {
                        oldCell.Lock.ExitWriteLock();
                    }
                }
                else
                {
                    cell.Lock.EnterWriteLock();
                    try
                    {
                        oldCell.Lock.EnterWriteLock();
                        try
                        {
                            RemoveInternal(obj);
                            AddInternal(obj, cell, key);
                        }
                        finally
                        {
                            oldCell.Lock.ExitWriteLock();
                        }
                    }
                    finally
                    {
                        cell.Lock.ExitWriteLock();
                    }
                }
            }
            else
            {
                cell.Lock.EnterWriteLock();
                try
                {
                    AddInternal(obj, cell, key);
                }
                finally
                {
                    cell.Lock.ExitWriteLock();
                }
            }
        }

        private void AddInternal(IGameObject obj, Cell cell, (long X, long Y) key)
        {
            obj.NextInGridCell = cell.Head;
            obj.PrevInGridCell = null;
            if (cell.Head != null) cell.Head.PrevInGridCell = obj;
            cell.Head = obj;
            obj.CurrentGridCellKey = key;
        }

        public void Remove(IGameObject obj)
        {
            if (obj.CurrentGridCellKey == null) return;
            if (_grid.TryGetValue(obj.CurrentGridCellKey.Value, out var cell))
            {
                cell.Lock.EnterWriteLock();
                try
                {
                    RemoveInternal(obj);
                }
                finally
                {
                    cell.Lock.ExitWriteLock();
                }
            }
        }

        private void RemoveInternal(IGameObject obj)
        {
            if (obj.CurrentGridCellKey == null) return;
            if (!_grid.TryGetValue(obj.CurrentGridCellKey.Value, out var cell)) return;

            if (cell.Head == obj) cell.Head = obj.NextInGridCell;
            if (obj.PrevInGridCell != null) obj.PrevInGridCell.NextInGridCell = obj.NextInGridCell;
            if (obj.NextInGridCell != null) obj.NextInGridCell.PrevInGridCell = obj.PrevInGridCell;

            obj.NextInGridCell = null;
            obj.PrevInGridCell = null;
            obj.CurrentGridCellKey = null;
        }

        public void Update(IGameObject obj, long oldX, long oldY)
        {
            // Add already handles movement and checking if the key changed
            Add(obj);
        }

        public List<IGameObject> GetObjectsInBox(Box2l box)
        {
            var results = new List<IGameObject>();
            GetObjectsInBox(box, results);
            return results;
        }

        public delegate void QueryCallback<TState>(IGameObject obj, ref TState state);

        /// <summary>
        /// Queries objects in a box without allocating a list, using a callback for each found object.
        /// </summary>
        public void QueryBox(Box2l box, Action<IGameObject> callback)
        {
            long startGX = GetGridCoord(box.Left);
            long startGY = GetGridCoord(box.Bottom);
            long endGX = GetGridCoord(box.Right);
            long endGY = GetGridCoord(box.Top);

            // Fast path for single-cell queries
            if (startGX == endGX && startGY == endGY)
            {
                if (_grid.TryGetValue((startGX, startGY), out var cell))
                {
                    cell.Lock.EnterReadLock();
                    try
                    {
                        var current = cell.Head;
                        while (current != null)
                        {
                            var next = current.NextInGridCell;
                            long ox = current.X;
                            long oy = current.Y;
                            if (ox >= box.Left && ox <= box.Right && oy >= box.Bottom && oy <= box.Top)
                            {
                                callback(current);
                            }
                            current = next;
                        }
                    }
                    finally
                    {
                        cell.Lock.ExitReadLock();
                    }
                }
                return;
            }

            if ((double)(endGX - startGX + 1) * (endGY - startGY + 1) > 1000000000) return;

            for (long x = startGX; x <= endGX; x++)
            {
                for (long y = startGY; y <= endGY; y++)
                {
                    if (_grid.TryGetValue((x, y), out var cell))
                    {
                        cell.Lock.EnterReadLock();
                        try
                        {
                            var current = cell.Head;
                            while (current != null)
                            {
                                var next = current.NextInGridCell;
                                long ox = current.X;
                                long oy = current.Y;
                                if (ox >= box.Left && ox <= box.Right && oy >= box.Bottom && oy <= box.Top)
                                {
                                    callback(current);
                                }
                                current = next;
                            }
                        }
                        finally
                        {
                            cell.Lock.ExitReadLock();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Queries objects in a box without allocating a list or closure, passing state to the callback.
        /// </summary>
        public void QueryBox<TState>(Box2l box, ref TState state, QueryCallback<TState> callback)
        {
            long startGX = GetGridCoord(box.Left);
            long startGY = GetGridCoord(box.Bottom);
            long endGX = GetGridCoord(box.Right);
            long endGY = GetGridCoord(box.Top);

            // Fast path for single-cell queries
            if (startGX == endGX && startGY == endGY)
            {
                if (_grid.TryGetValue((startGX, startGY), out var cell))
                {
                    cell.Lock.EnterReadLock();
                    try
                    {
                        var current = cell.Head;
                        while (current != null)
                        {
                            var next = current.NextInGridCell;
                            long ox = current.X;
                            long oy = current.Y;
                            if (ox >= box.Left && ox <= box.Right && oy >= box.Bottom && oy <= box.Top)
                            {
                                callback(current, ref state);
                            }
                            current = next;
                        }
                    }
                    finally
                    {
                        cell.Lock.ExitReadLock();
                    }
                }
                return;
            }

            if ((double)(endGX - startGX + 1) * (endGY - startGY + 1) > 1000000000) return;

            for (long x = startGX; x <= endGX; x++)
            {
                for (long y = startGY; y <= endGY; y++)
                {
                    if (_grid.TryGetValue((x, y), out var cell))
                    {
                        cell.Lock.EnterReadLock();
                        try
                        {
                            var current = cell.Head;
                            while (current != null)
                            {
                                var next = current.NextInGridCell;
                                long ox = current.X;
                                long oy = current.Y;
                                if (ox >= box.Left && ox <= box.Right && oy >= box.Bottom && oy <= box.Top)
                                {
                                    callback(current, ref state);
                                }
                                current = next;
                            }
                        }
                        finally
                        {
                            cell.Lock.ExitReadLock();
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
                if (cell.Head == null)
                {
                    cell.Lock.EnterWriteLock();
                    try
                    {
                        if (cell.Head == null)
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
                        cell.Lock.ExitWriteLock();
                    }
                }
            }
        }

        public void GetObjectsInBox(Box2l box, List<IGameObject> results)
        {
            long startGX = GetGridCoord(box.Left);
            long startGY = GetGridCoord(box.Bottom);
            long endGX = GetGridCoord(box.Right);
            long endGY = GetGridCoord(box.Top);

            // Fast path for single-cell queries
            if (startGX == endGX && startGY == endGY)
            {
                if (_grid.TryGetValue((startGX, startGY), out var cell))
                {
                    cell.Lock.EnterReadLock();
                    try
                    {
                        var current = cell.Head;
                        while (current != null)
                        {
                            var next = current.NextInGridCell;
                            long ox = current.X;
                            long oy = current.Y;
                            if (ox >= box.Left && ox <= box.Right && oy >= box.Bottom && oy <= box.Top)
                            {
                                results.Add(current);
                            }
                            current = next;
                        }
                    }
                    finally
                    {
                        cell.Lock.ExitReadLock();
                    }
                }
                return;
            }

            // Prevent DoS via huge search area
            if ((double)(endGX - startGX + 1) * (endGY - startGY + 1) > 1000000000)
            {
                return;
            }

            for (long x = startGX; x <= endGX; x++)
            {
                for (long y = startGY; y <= endGY; y++)
                {
                    if (_grid.TryGetValue((x, y), out var cell))
                    {
                        cell.Lock.EnterReadLock();
                        try
                        {
                            var current = cell.Head;
                            while (current != null)
                            {
                                var next = current.NextInGridCell;
                                long ox = current.X;
                                long oy = current.Y;
                                if (ox >= box.Left && ox <= box.Right && oy >= box.Bottom && oy <= box.Top)
                                {
                                    results.Add(current);
                                }
                                current = next;
                            }
                        }
                        finally
                        {
                            cell.Lock.ExitReadLock();
                        }
                    }
                }
            }
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
            foreach (var cell in _grid.Values) cell.Dispose();
            foreach (var cell in _cellPool) cell.Dispose();
            _grid.Clear();
            _cellPool.Clear();
            GC.SuppressFinalize(this);
        }

        ~SpatialGrid()
        {
            Dispose();
        }
    }
