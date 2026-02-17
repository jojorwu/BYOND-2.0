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
            public volatile IGameObject[] Objects = System.Array.Empty<IGameObject>();
            public readonly object Lock = new();

            public void Clear()
            {
                lock (Lock)
                {
                    Objects = System.Array.Empty<IGameObject>();
                }
            }
        }

        public void Shrink()
        {
            CleanupEmptyCells();
            if (_seenHashSet.IsValueCreated && _seenHashSet.Value!.Count > 1000)
            {
                _seenHashSet.Value.Clear();
                _seenHashSet.Value.TrimExcess();
            }
        }

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
                var current = cell.Objects;
                if (System.Array.IndexOf(current, obj) == -1)
                {
                    var updated = new IGameObject[current.Length + 1];
                    System.Array.Copy(current, updated, current.Length);
                    updated[current.Length] = obj;
                    cell.Objects = updated;
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
                    var current = cell.Objects;
                    int index = System.Array.IndexOf(current, obj);
                    if (index != -1)
                    {
                        var updated = new IGameObject[current.Length - 1];
                        System.Array.Copy(current, 0, updated, 0, index);
                        System.Array.Copy(current, index + 1, updated, index, current.Length - index - 1);
                        cell.Objects = updated;
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
                        var current = oldCell.Objects;
                        int index = System.Array.IndexOf(current, obj);
                        if (index != -1)
                        {
                            var updated = new IGameObject[current.Length - 1];
                            System.Array.Copy(current, 0, updated, 0, index);
                            System.Array.Copy(current, index + 1, updated, index, current.Length - index - 1);
                            oldCell.Objects = updated;
                        }
                    }
                }

                // Add to new
                long newKey = ((long)newGX << 32) | (uint)newGY;
                var newCell = GetOrCreateCell(newKey);
                lock (newCell.Lock)
                {
                    var current = newCell.Objects;
                    if (System.Array.IndexOf(current, obj) == -1)
                    {
                        var updated = new IGameObject[current.Length + 1];
                        System.Array.Copy(current, updated, current.Length);
                        updated[current.Length] = obj;
                        newCell.Objects = updated;
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

        /// <summary>
        /// Queries objects in a box without allocating a list, using a callback for each found object.
        /// </summary>
        public void QueryBox(Box2i box, Action<IGameObject> callback)
        {
            int startGX = box.Left / _cellSize;
            int startGY = box.Bottom / _cellSize;
            int endGX = box.Right / _cellSize;
            int endGY = box.Top / _cellSize;

            // Fast path for single-cell queries
            if (startGX == endGX && startGY == endGY)
            {
                long key = ((long)startGX << 32) | (uint)startGY;
                if (_grid.TryGetValue(key, out var cell))
                {
                    var objects = cell.Objects;
                    for (int i = 0; i < objects.Length; i++)
                    {
                        var obj = objects[i];
                        int ox = obj.X;
                        int oy = obj.Y;
                        if (ox >= box.Left && ox <= box.Right && oy >= box.Bottom && oy <= box.Top)
                        {
                            callback(obj);
                        }
                    }
                }
                return;
            }

            var seen = _seenHashSet.Value!;
            seen.Clear();

            if ((long)(endGX - startGX + 1) * (endGY - startGY + 1) > 10000) return;

            for (int x = startGX; x <= endGX; x++)
            {
                for (int y = startGY; y <= endGY; y++)
                {
                    long key = ((long)x << 32) | (uint)y;
                    if (_grid.TryGetValue(key, out var cell))
                    {
                        var objects = cell.Objects;
                        for (int i = 0; i < objects.Length; i++)
                        {
                            var obj = objects[i];
                            int ox = obj.X;
                            int oy = obj.Y;
                            if (ox >= box.Left && ox <= box.Right && oy >= box.Bottom && oy <= box.Top && seen.Add(obj.Id))
                            {
                                callback(obj);
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
                if (cell.Objects.Length == 0)
                {
                    lock (cell.Lock)
                    {
                        if (cell.Objects.Length == 0)
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
            int startGY = box.Bottom / _cellSize;
            int endGX = box.Right / _cellSize;
            int endGY = box.Top / _cellSize;

            // Fast path for single-cell queries
            if (startGX == endGX && startGY == endGY)
            {
                long key = ((long)startGX << 32) | (uint)startGY;
                if (_grid.TryGetValue(key, out var cell))
                {
                    // Lock-free snapshot read
                    var objects = cell.Objects;
                    int count = objects.Length;
                    for (int i = 0; i < count; i++)
                    {
                        var obj = objects[i];
                        int ox = obj.X;
                        int oy = obj.Y;
                        if (ox >= box.Left && ox <= box.Right && oy >= box.Bottom && oy <= box.Top)
                        {
                            results.Add(obj);
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
                        // Lock-free snapshot read
                        var objects = cell.Objects;
                        int count = objects.Length;
                        for (int i = 0; i < count; i++)
                        {
                            var obj = objects[i];
                            int ox = obj.X;
                            int oy = obj.Y;
                            // We use seen.Add to avoid duplicates if an object spans multiple cells (though currently they only reside in one)
                            if (ox >= box.Left && ox <= box.Right && oy >= box.Bottom && oy <= box.Top && seen.Add(obj.Id))
                            {
                                results.Add(obj);
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
