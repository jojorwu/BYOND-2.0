using System;
using System.Collections.Generic;
using Robust.Shared.Maths;

namespace Shared
{
    public class SpatialGrid
    {
        private readonly int _cellSize;
        private readonly Dictionary<Vector2i, HashSet<IGameObject>> _grid = new();

        public SpatialGrid(int cellSize = 16)
        {
            if (cellSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be positive.");
            _cellSize = cellSize;
        }

        private Vector2i WorldToGrid(int x, int y)
        {
            return new Vector2i((int)Math.Floor((double)x / _cellSize), (int)Math.Floor((double)y / _cellSize));
        }

        public void Add(IGameObject obj)
        {
            var gridCoords = WorldToGrid(obj.X, obj.Y);
            if (!_grid.TryGetValue(gridCoords, out var cell))
            {
                cell = new HashSet<IGameObject>();
                _grid[gridCoords] = cell;
            }
            cell.Add(obj);
        }

        public void Remove(IGameObject obj)
        {
            var gridCoords = WorldToGrid(obj.X, obj.Y);
            if (_grid.TryGetValue(gridCoords, out var cell))
            {
                cell.Remove(obj);
                if (cell.Count == 0)
                {
                    _grid.Remove(gridCoords);
                }
            }
        }

        public void Update(IGameObject obj, int oldX, int oldY)
        {
            var oldGridCoords = WorldToGrid(oldX, oldY);
            var newGridCoords = WorldToGrid(obj.X, obj.Y);

            if (oldGridCoords == newGridCoords)
                return;

            if (_grid.TryGetValue(oldGridCoords, out var oldCell))
            {
                oldCell.Remove(obj);
                if (oldCell.Count == 0)
                {
                    _grid.Remove(oldGridCoords);
                }
            }

            Add(obj);
        }

        public IEnumerable<IGameObject> Query(int x, int y, int radius)
        {
            var results = new HashSet<IGameObject>();
            var minGridX = (int)Math.Floor((double)(x - radius) / _cellSize);
            var maxGridX = (int)Math.Floor((double)(x + radius) / _cellSize);
            var minGridY = (int)Math.Floor((double)(y - radius) / _cellSize);
            var maxGridY = (int)Math.Floor((double)(y + radius) / _cellSize);

            for (int gx = minGridX; gx <= maxGridX; gx++)
            {
                for (int gy = minGridY; gy <= maxGridY; gy++)
                {
                    var gridCoords = new Vector2i(gx, gy);
                    if (_grid.TryGetValue(gridCoords, out var cell))
                    {
                        foreach (var obj in cell)
                        {
                            results.Add(obj);
                        }
                    }
                }
            }

            return results;
        }

        public void Clear()
        {
            _grid.Clear();
        }
    }
}
