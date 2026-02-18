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
        private int GetGridCoord(int val)
        {
            // Simplified floor division for both positive and negative values
            return (val < 0 ? val - _cellSize + 1 : val) / _cellSize;
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
            int oldGX = GetGridCoord(oldX);
            int oldGY = GetGridCoord(oldY);
            int newGX = GetGridCoord(obj.X);
            int newGY = GetGridCoord(obj.Y);

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

        public delegate void QueryCallback<TState>(IGameObject obj, ref TState state);

        /// <summary>
        /// Queries objects in a box without allocating a list, using a callback for each found object.
        /// </summary>
        public void QueryBox(Box2i box, Action<IGameObject> callback)
        {
            var internalCallback = new QueryState(callback);
            QueryBoxInternal(box, ref internalCallback, (IGameObject obj, ref QueryState s) => s.Action(obj));
        }

        private struct QueryState
        {
            public Action<IGameObject> Action;
            public QueryState(Action<IGameObject> action) => Action = action;
        }

        /// <summary>
        /// Queries objects in a box without allocating a list or closure, passing state to the callback.
        /// </summary>
        public void QueryBox<TState>(Box2i box, ref TState state, QueryCallback<TState> callback)
        {
            QueryBoxInternal(box, ref state, callback);
        }

        private void QueryBoxInternal<TState>(Box2i box, ref TState state, QueryCallback<TState> callback)
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
                    var objects = cell.Objects;
                    for (int i = 0; i < objects.Length; i++)
                    {
                        var obj = objects[i];
                        if (box.Contains(obj.X, obj.Y))
                        {
                            callback(obj, ref state);
                        }
                    }
                }
                return;
            }

            // Prevent DoS via huge search area
            if ((long)(endGX - startGX + 1) * (endGY - startGY + 1) > 10000) return;

            var seen = _seenHashSet.Value!;
            seen.Clear();

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
                            if (box.Contains(obj.X, obj.Y) && seen.Add(obj.Id))
                            {
                                callback(obj, ref state);
                            }
                        }
                    }
                }
            }
        }

        public void CleanupEmptyCells()
        {
            // Use a two-pass approach to avoid holding locks while iterating the concurrent dictionary if possible,
            // or just rely on TryRemove which is thread-safe.
            foreach (var kvp in _grid)
            {
                var cell = kvp.Value;
                if (cell.Objects.Length == 0)
                {
                    // Double-check with lock to ensure no one is adding to it
                    lock (cell.Lock)
                    {
                        if (cell.Objects.Length == 0)
                        {
                            _grid.TryRemove(kvp.Key, out _);
                        }
                    }
                }
            }
        }

        public void GetObjectsInBox(Box2i box, List<IGameObject> results)
        {
            QueryBoxInternal(box, ref results, (IGameObject obj, ref List<IGameObject> res) => res.Add(obj));
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
