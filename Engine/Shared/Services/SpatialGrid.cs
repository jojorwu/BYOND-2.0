using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.Threading;
using Robust.Shared.Maths;

namespace Shared
{
    public class SpatialGrid : IDisposable
    {
        private static readonly ThreadLocal<HashSet<int>> _seenHashSet = new(() => new HashSet<int>());
        private readonly Dictionary<long, List<IGameObject>> _grid = new();
        private readonly int _cellSize;
        private readonly ReaderWriterLockSlim _lock = new();

        public SpatialGrid(int cellSize = 16)
        {
            _cellSize = cellSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long GetCellKey(int x, int y)
        {
            return ((long)(x / _cellSize) << 32) | (uint)(y / _cellSize);
        }

        public void Add(IGameObject obj)
        {
            _lock.EnterWriteLock();
            try
            {
                var key = GetCellKey(obj.X, obj.Y);
                if (!_grid.TryGetValue(key, out var cell))
                {
                    cell = new List<IGameObject>();
                    _grid[key] = cell;
                }
                cell.Add(obj);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Remove(IGameObject obj)
        {
            _lock.EnterWriteLock();
            try
            {
                var key = GetCellKey(obj.X, obj.Y);
                if (_grid.TryGetValue(key, out var cell))
                {
                    int index = cell.IndexOf(obj);
                    if (index != -1)
                    {
                        int last = cell.Count - 1;
                        cell[index] = cell[last];
                        cell.RemoveAt(last);
                    }
                    if (cell.Count == 0)
                    {
                        _grid.Remove(key);
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
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
                _lock.EnterWriteLock();
                try
                {
                    // Manual remove from old
                    long oldKey = ((long)oldGX << 32) | (uint)oldGY;
                    if (_grid.TryGetValue(oldKey, out var oldCell))
                    {
                        int index = oldCell.IndexOf(obj);
                        if (index != -1)
                        {
                            int last = oldCell.Count - 1;
                            oldCell[index] = oldCell[last];
                            oldCell.RemoveAt(last);
                        }
                        if (oldCell.Count == 0) _grid.Remove(oldKey);
                    }
                    // Manual add to new
                    long newKey = ((long)newGX << 32) | (uint)newGY;
                    if (!_grid.TryGetValue(newKey, out var newCell))
                    {
                        newCell = new List<IGameObject>();
                        _grid[newKey] = newCell;
                    }
                    newCell.Add(obj);
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }

        public List<IGameObject> GetObjectsInBox(Box2i box)
        {
            var results = new List<IGameObject>();
            GetObjectsInBox(box, results);
            return results;
        }

        public void GetObjectsInBox(Box2i box, List<IGameObject> results)
        {
            var seen = _seenHashSet.Value!;
            seen.Clear();

            int startX = box.Left / _cellSize;
            int startY = box.Top / _cellSize;
            int endX = box.Right / _cellSize;
            int endY = box.Bottom / _cellSize;

            // Prevent DoS via huge search area
            if ((long)(endX - startX + 1) * (endY - startY + 1) > 10000)
            {
                return;
            }

            _lock.EnterReadLock();
            try
            {
                for (int x = startX; x <= endX; x++)
                {
                    for (int y = startY; y <= endY; y++)
                    {
                        long key = ((long)x << 32) | (uint)y;
                        if (_grid.TryGetValue(key, out var cell))
                        {
                            for (int i = 0; i < cell.Count; i++)
                            {
                                var obj = cell[i];
                                if (box.Contains(new Vector2i(obj.X, obj.Y)) && seen.Add(obj.Id))
                                {
                                    results.Add(obj);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void Dispose()
        {
            _lock.Dispose();
        }
    }
}
