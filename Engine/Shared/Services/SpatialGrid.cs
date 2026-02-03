using System;
using System.Collections.Generic;
using System.Threading;
using Robust.Shared.Maths;

namespace Shared
{
    public class SpatialGrid : IDisposable
    {
        private readonly Dictionary<(int, int), List<IGameObject>> _grid = new();
        private readonly int _cellSize;
        private readonly ReaderWriterLockSlim _lock = new();

        public SpatialGrid(int cellSize = 16)
        {
            _cellSize = cellSize;
        }

        private (int, int) GetCellCoords(int x, int y)
        {
            return (x / _cellSize, y / _cellSize);
        }

        public void Add(IGameObject obj)
        {
            _lock.EnterWriteLock();
            try
            {
                var coords = GetCellCoords(obj.X, obj.Y);
                if (!_grid.TryGetValue(coords, out var cell))
                {
                    cell = new List<IGameObject>();
                    _grid[coords] = cell;
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
                var coords = GetCellCoords(obj.X, obj.Y);
                if (_grid.TryGetValue(coords, out var cell))
                {
                    cell.Remove(obj);
                    if (cell.Count == 0)
                    {
                        _grid.Remove(coords);
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
            var oldCoords = GetCellCoords(oldX, oldY);
            var newCoords = GetCellCoords(obj.X, obj.Y);

            if (oldCoords != newCoords)
            {
                Remove(obj);
                Add(obj);
            }
        }

        public List<IGameObject> GetObjectsInBox(Box2i box)
        {
            var results = new List<IGameObject>();
            var seen = new HashSet<int>(); // Use ID to avoid object references in HashSet if possible, but IGameObject works too.
            var start = GetCellCoords(box.Left, box.Top);
            var end = GetCellCoords(box.Right, box.Bottom);

            _lock.EnterReadLock();
            try
            {
                for (int x = start.Item1; x <= end.Item1; x++)
                {
                    for (int y = start.Item2; y <= end.Item2; y++)
                    {
                        if (_grid.TryGetValue((x, y), out var cell))
                        {
                            foreach (var obj in cell)
                            {
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
            return results;
        }

        public void Dispose()
        {
            _lock.Dispose();
        }
    }
}
