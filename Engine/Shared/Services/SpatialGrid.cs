using System.Collections.Generic;
using Robust.Shared.Maths;

namespace Shared
{
    public class SpatialGrid
    {
        private readonly Dictionary<(int, int), List<IGameObject>> _grid = new();
        private readonly int _cellSize;

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
            var coords = GetCellCoords(obj.X, obj.Y);
            if (!_grid.TryGetValue(coords, out var cell))
            {
                cell = new List<IGameObject>();
                _grid[coords] = cell;
            }
            cell.Add(obj);
        }

        public void Remove(IGameObject obj)
        {
            var coords = GetCellCoords(obj.X, obj.Y);
            if (_grid.TryGetValue(coords, out var cell))
            {
                cell.Remove(obj);
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

        public IEnumerable<IGameObject> GetObjectsInBox(Box2i box)
        {
            var seen = new HashSet<IGameObject>();
            var start = GetCellCoords(box.Left, box.Top);
            var end = GetCellCoords(box.Right, box.Bottom);

            for (int x = start.Item1; x <= end.Item1; x++)
            {
                for (int y = start.Item2; y <= end.Item2; y++)
                {
                    if (_grid.TryGetValue((x, y), out var cell))
                    {
                        foreach (var obj in cell)
                        {
                            if (box.Contains(new Vector2i(obj.X, obj.Y)) && seen.Add(obj))
                            {
                                yield return obj;
                            }
                        }
                    }
                }
            }
        }
    }
}
