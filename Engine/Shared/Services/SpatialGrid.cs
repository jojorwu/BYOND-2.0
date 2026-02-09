using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.Threading;
using Robust.Shared.Maths;
using System.Collections.Concurrent;

namespace Shared
{
    public class SpatialGrid : IDisposable
    {
        private class Cell
        {
            public readonly List<IGameObject> Objects = new();
            public readonly object Lock = new();
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

        public void Add(IGameObject obj)
        {
            var key = GetCellKey(obj.X, obj.Y);
            var cell = _grid.GetOrAdd(key, _ => new Cell());
            lock (cell.Lock)
            {
                cell.Objects.Add(obj);
            }
        }

        public void Remove(IGameObject obj)
        {
            var key = GetCellKey(obj.X, obj.Y);
            if (_grid.TryGetValue(key, out var cell))
            {
                lock (cell.Lock)
                {
                    int index = cell.Objects.IndexOf(obj);
                    if (index != -1)
                    {
                        int last = cell.Objects.Count - 1;
                        cell.Objects[index] = cell.Objects[last];
                        cell.Objects.RemoveAt(last);
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
                        int index = oldCell.Objects.IndexOf(obj);
                        if (index != -1)
                        {
                            int last = oldCell.Objects.Count - 1;
                            oldCell.Objects[index] = oldCell.Objects[last];
                            oldCell.Objects.RemoveAt(last);
                        }
                    }
                }

                // Add to new
                long newKey = ((long)newGX << 32) | (uint)newGY;
                var newCell = _grid.GetOrAdd(newKey, _ => new Cell());
                lock (newCell.Lock)
                {
                    newCell.Objects.Add(obj);
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

            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    long key = ((long)x << 32) | (uint)y;
                    if (_grid.TryGetValue(key, out var cell))
                    {
                        lock (cell.Lock)
                        {
                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                var obj = cell.Objects[i];
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
        }
    }
}
