using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.Threading;
using Robust.Shared.Maths;
using System.Collections.Concurrent;
using System.Buffers;
using Shared.Interfaces;

namespace Shared
{
    public class SpatialGrid : IDisposable, IShrinkable
    {
        private class Cell
        {
            public readonly List<IGameObject> Objects = new();
            public readonly Dictionary<IGameObject, int> ObjectToIndex = new();
            public readonly object Lock = new();

            public void Clear()
            {
                lock (Lock)
                {
                    Objects.Clear();
                    ObjectToIndex.Clear();
                }
            }
        }

        public void Shrink() => CleanupEmptyCells();

        private static readonly ThreadLocal<HashSet<int>> _seenHashSet = new(() => new HashSet<int>());
        private readonly ConcurrentDictionary<long, Cell> _grid = new();
        private readonly int _cellSize;

        public SpatialGrid(int cellSize = 16)
        {
            _cellSize = cellSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long GetCellKey(int x, int y)
        {
            return ((long)(x / _cellSize) << 32) | (uint)(y / _cellSize);
        }

        private Cell GetOrCreateCell(long key)
        {
            while (true)
            {
                var cell = _grid.GetOrAdd(key, _ => new Cell());
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
            lock (cell.Lock)
            {
                if (cell.ObjectToIndex.TryAdd(obj, cell.Objects.Count))
                {
                    cell.Objects.Add(obj);
                }
            }
        }

        public void Remove(IGameObject obj)
        {
            var key = GetCellKey(obj.X, obj.Y);
            if (_grid.TryGetValue(key, out var cell))
            {
                lock (cell.Lock)
                {
                    if (cell.ObjectToIndex.Remove(obj, out int index))
                    {
                        int lastIndex = cell.Objects.Count - 1;
                        if (index != lastIndex)
                        {
                            var lastObj = cell.Objects[lastIndex];
                            cell.Objects[index] = lastObj;
                            cell.ObjectToIndex[lastObj] = index;
                        }
                        cell.Objects.RemoveAt(lastIndex);
                    }
                }
            }
        }

        public void Update(IGameObject obj, int oldX, int oldY)
        {
            int oldGX = oldX / _cellSize;
            int oldGY = oldY / _cellSize;
            int newGX = obj.X / _cellSize;
            int newGY = obj.Y / _cellSize;

            if (oldGX != newGX || oldGY != newGY)
            {
                // Remove from old
                long oldKey = ((long)oldGX << 32) | (uint)oldGY;
                if (_grid.TryGetValue(oldKey, out var oldCell))
                {
                    lock (oldCell.Lock)
                    {
                        if (oldCell.ObjectToIndex.Remove(obj, out int index))
                        {
                            int lastIndex = oldCell.Objects.Count - 1;
                            if (index != lastIndex)
                            {
                                var lastObj = oldCell.Objects[lastIndex];
                                oldCell.Objects[index] = lastObj;
                                oldCell.ObjectToIndex[lastObj] = index;
                            }
                            oldCell.Objects.RemoveAt(lastIndex);
                        }
                    }
                }

                // Add to new
                long newKey = ((long)newGX << 32) | (uint)newGY;
                var newCell = GetOrCreateCell(newKey);
                lock (newCell.Lock)
                {
                    if (newCell.ObjectToIndex.TryAdd(obj, newCell.Objects.Count))
                    {
                        newCell.Objects.Add(obj);
                    }
                }
            }
        }

        public List<IGameObject> GetObjectsInBox(Box2i box)
        {
            var results = new List<IGameObject>();
            GetObjectsInBox(box, results);
            return results;
        }

        public void CleanupEmptyCells()
        {
            foreach (var kvp in _grid)
            {
                var cell = kvp.Value;
                if (cell.Objects.Count == 0)
                {
                    lock (cell.Lock)
                    {
                        if (cell.Objects.Count == 0)
                        {
                            if (_grid.TryGetValue(kvp.Key, out var current) && current == cell)
                            {
                                _grid.TryRemove(kvp.Key, out _);
                            }
                        }
                    }
                }
            }
        }

        public void GetObjectsInBox(Box2i box, List<IGameObject> results)
        {
            int startGX = box.Left / _cellSize;
            int startGY = box.Top / _cellSize;
            int endGX = box.Right / _cellSize;
            int endGY = box.Bottom / _cellSize;

            // Fast path for single-cell queries
            if (startGX == endGX && startGY == endGY)
            {
                long key = ((long)startGX << 32) | (uint)startGY;
                if (_grid.TryGetValue(key, out var cell))
                {
                    lock (cell.Lock)
                    {
                        var objects = cell.Objects;
                        int count = objects.Count;
                        for (int i = 0; i < count; i++)
                        {
                            var obj = objects[i];
                            if (box.Contains(new Vector2i(obj.X, obj.Y)))
                            {
                                results.Add(obj);
                            }
                        }
                    }
                }
                return;
            }

            var seen = _seenHashSet.Value!;
            seen.Clear();

            // Prevent DoS via huge search area
            if ((long)(endGX - startGX + 1) * (endGY - startGY + 1) > 10000)
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
                            var objects = cell.Objects;
                            int count = objects.Count;
                            for (int i = 0; i < count; i++)
                            {
                                var obj = objects[i];
                                // We use seen.Add to avoid duplicates if an object spans multiple cells (though currently they only reside in one)
                                if (box.Contains(new Vector2i(obj.X, obj.Y)) && seen.Add(obj.Id))
                                {
                                    results.Add(obj);
                                }
                            }
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            _grid.Clear();
            GC.SuppressFinalize(this);
        }

        ~SpatialGrid()
        {
            Dispose();
        }
    }
}
