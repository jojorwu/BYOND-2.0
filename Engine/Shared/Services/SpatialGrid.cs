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
        private class Cell
        {
            public IGameObject? Head;
            public readonly object Lock = new();

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
        }

        public void Shrink()
        {
            CleanupEmptyCells();
        }

        private readonly ConcurrentDictionary<long, Cell> _grid = new();
        private readonly ConcurrentStack<Cell> _cellPool = new();
        private readonly int _cellSize;
        private readonly ILogger<SpatialGrid> _logger;

        public SpatialGrid(ILogger<SpatialGrid> logger, int cellSize = 32)
        {
            _logger = logger;
            _cellSize = cellSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetGridCoord(int val)
        {
            return val >= 0 ? val / _cellSize : (val - _cellSize + 1) / _cellSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long GetCellKey(int x, int y)
        {
            return ((long)GetGridCoord(x) << 32) | (uint)GetGridCoord(y);
        }

        private Cell GetOrCreateCell(long key)
        {
            while (true)
            {
                var cell = _grid.GetOrAdd(key, _ => {
                    if (!_cellPool.TryPop(out var pooled)) pooled = new Cell();
                    return pooled;
                });
                lock (cell.Lock)
                {
                    if (_grid.TryGetValue(key, out var current) && current == cell)
                    {
                        return cell;
                    }
                }
            }
        }

        public void Add(IGameObject obj)
        {
            var key = GetCellKey(obj.X, obj.Y);
            var cell = GetOrCreateCell(key);

            if (obj.CurrentGridCellKey != null)
            {
                long oldKey = obj.CurrentGridCellKey.Value;
                if (oldKey == key) return;

                var oldCell = GetOrCreateCell(oldKey);
                // Consistent lock ordering to avoid deadlocks
                if (oldKey < key)
                {
                    lock (oldCell.Lock) lock (cell.Lock)
                    {
                        RemoveInternal(obj);
                        AddInternal(obj, cell, key);
                    }
                }
                else
                {
                    lock (cell.Lock) lock (oldCell.Lock)
                    {
                        RemoveInternal(obj);
                        AddInternal(obj, cell, key);
                    }
                }
            }
            else
            {
                lock (cell.Lock)
                {
                    AddInternal(obj, cell, key);
                }
            }
        }

        private void AddInternal(IGameObject obj, Cell cell, long key)
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
                lock (cell.Lock)
                {
                    RemoveInternal(obj);
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

        public void Update(IGameObject obj, int oldX, int oldY)
        {
            // Add already handles movement and checking if the key changed
            Add(obj);
        }

        public List<IGameObject> GetObjectsInBox(Box2i box)
        {
            var results = new List<IGameObject>();
            GetObjectsInBox(box, results);
            return results;
        }

        public delegate void QueryCallback<TState>(IGameObject obj, ref TState state);

        /// <summary>
        /// Queries objects in a box without allocating a list, using a callback for each found object.
        /// </summary>
        public void QueryBox(Box2i box, Action<IGameObject> callback)
        {
            int startGX = GetGridCoord(box.Left);
            int startGY = GetGridCoord(box.Bottom);
            int endGX = GetGridCoord(box.Right);
            int endGY = GetGridCoord(box.Top);

            // Fast path for single-cell queries
            if (startGX == endGX && startGY == endGY)
            {
                long key = ((long)startGX << 32) | (uint)startGY;
                if (_grid.TryGetValue(key, out var cell))
                {
                    lock (cell.Lock)
                    {
                        var current = cell.Head;
                        while (current != null)
                        {
                            var next = current.NextInGridCell;
                            int ox = current.X;
                            int oy = current.Y;
                            if (ox >= box.Left && ox <= box.Right && oy >= box.Bottom && oy <= box.Top)
                            {
                                callback(current);
                            }
                            current = next;
                        }
                    }
                }
                return;
            }

            if ((long)(endGX - startGX + 1) * (endGY - startGY + 1) > 10000000000) return;

            for (int x = startGX; x <= endGX; x++)
            {
                for (int y = startGY; y <= endGY; y++)
                {
                    long key = ((long)x << 32) | (uint)y;
                    if (_grid.TryGetValue(key, out var cell))
                    {
                        lock (cell.Lock)
                        {
                            var current = cell.Head;
                            while (current != null)
                            {
                            var next = current.NextInGridCell;
                                int ox = current.X;
                                int oy = current.Y;
                                if (ox >= box.Left && ox <= box.Right && oy >= box.Bottom && oy <= box.Top)
                                {
                                    callback(current);
                                }
                            current = next;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Queries objects in a box without allocating a list or closure, passing state to the callback.
        /// </summary>
        public void QueryBox<TState>(Box2i box, ref TState state, QueryCallback<TState> callback)
        {
            int startGX = GetGridCoord(box.Left);
            int startGY = GetGridCoord(box.Bottom);
            int endGX = GetGridCoord(box.Right);
            int endGY = GetGridCoord(box.Top);

            // Fast path for single-cell queries
            if (startGX == endGX && startGY == endGY)
            {
                long key = ((long)startGX << 32) | (uint)startGY;
                if (_grid.TryGetValue(key, out var cell))
                {
                    lock (cell.Lock)
                    {
                        var current = cell.Head;
                        while (current != null)
                        {
                            var next = current.NextInGridCell;
                            int ox = current.X;
                            int oy = current.Y;
                            if (ox >= box.Left && ox <= box.Right && oy >= box.Bottom && oy <= box.Top)
                            {
                                callback(current, ref state);
                            }
                            current = next;
                        }
                    }
                }
                return;
            }

            if ((long)(endGX - startGX + 1) * (endGY - startGY + 1) > 10000000000) return;

            for (int x = startGX; x <= endGX; x++)
            {
                for (int y = startGY; y <= endGY; y++)
                {
                    long key = ((long)x << 32) | (uint)y;
                    if (_grid.TryGetValue(key, out var cell))
                    {
                        lock (cell.Lock)
                        {
                            var current = cell.Head;
                            while (current != null)
                            {
                            var next = current.NextInGridCell;
                                int ox = current.X;
                                int oy = current.Y;
                                if (ox >= box.Left && ox <= box.Right && oy >= box.Bottom && oy <= box.Top)
                                {
                                    callback(current, ref state);
                                }
                            current = next;
                            }
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
                    lock (cell.Lock)
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
                }
            }
        }

        public void GetObjectsInBox(Box2i box, List<IGameObject> results)
        {
            int startGX = GetGridCoord(box.Left);
            int startGY = GetGridCoord(box.Bottom);
            int endGX = GetGridCoord(box.Right);
            int endGY = GetGridCoord(box.Top);

            // Fast path for single-cell queries
            if (startGX == endGX && startGY == endGY)
            {
                long key = ((long)startGX << 32) | (uint)startGY;
                if (_grid.TryGetValue(key, out var cell))
                {
                    lock (cell.Lock)
                    {
                        var current = cell.Head;
                        while (current != null)
                        {
                            var next = current.NextInGridCell;
                            int ox = current.X;
                            int oy = current.Y;
                            if (ox >= box.Left && ox <= box.Right && oy >= box.Bottom && oy <= box.Top)
                            {
                                results.Add(current);
                            }
                            current = next;
                        }
                    }
                }
                return;
            }

            // Prevent DoS via huge search area
            if ((long)(endGX - startGX + 1) * (endGY - startGY + 1) > 10000000000)
            {
                return;
            }

            for (int x = startGX; x <= endGX; x++)
            {
                for (int y = startGY; y <= endGY; y++)
                {
                    long key = ((long)x << 32) | (uint)y;
                    if (_grid.TryGetValue(key, out var cell))
                    {
                        lock (cell.Lock)
                        {
                            var current = cell.Head;
                            while (current != null)
                            {
                            var next = current.NextInGridCell;
                                int ox = current.X;
                                int oy = current.Y;
                                if (ox >= box.Left && ox <= box.Right && oy >= box.Bottom && oy <= box.Top)
                                {
                                    results.Add(current);
                                }
                            current = next;
                            }
                        }
                    }
                }
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
