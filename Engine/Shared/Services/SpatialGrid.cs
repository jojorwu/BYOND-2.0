using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;
using System;
using System.Collections.Generic;
using System.Threading;
using Robust.Shared.Maths;
using System.Collections.Concurrent;
using System.Buffers;
using System.Collections.Frozen;
using Shared.Interfaces;
using Microsoft.Extensions.Logging;
using Shared.Services;

namespace Shared;
    public class SpatialGrid : EngineService, IDisposable, IShrinkable, IFreezable
    {
        private class Cell : IDisposable
        {
            public volatile IGameObject[] Objects = Array.Empty<IGameObject>();
            public int Count;
            public SpinLock Lock = new(false);

            public void Clear()
            {
                var objs = Objects;
                for (int i = 0; i < Count; i++)
                {
                    var obj = objs[i];
                    if (obj != null)
                    {
                        obj.SpatialGridIndex = -1;
                        obj.CurrentGridCellKey = null;
                    }
                }
                if (objs.Length > 0)
                {
                    ArrayPool<IGameObject>.Shared.Return(objs, true);
                    Objects = Array.Empty<IGameObject>();
                }
                Count = 0;
            }

            public void Dispose()
            {
                Clear();
            }
        }

        public void Shrink()
        {
            CleanupEmptyCells();

            // Return detached COW arrays to the pool
            while (_replacedArrays.TryDequeue(out var array))
            {
                ArrayPool<IGameObject>.Shared.Return(array, true);
            }
        }

        private class Layer
        {
            public readonly ConcurrentDictionary<ulong, Cell> Grid = new();
            public readonly ConcurrentQueue<ulong> EmptyCellKeys = new();
        }

        private readonly ConcurrentDictionary<long, Layer> _layers = new();
        private volatile FrozenDictionary<long, Layer> _frozenLayers = FrozenDictionary<long, Layer>.Empty;

        public void Freeze()
        {
            _frozenLayers = _layers.ToFrozenDictionary();
        }

        private Layer GetLayer(long z)
        {
            if (_frozenLayers.TryGetValue(z, out var layer)) return layer;
            return _layers.GetOrAdd(z, _ => new Layer());
        }

        private readonly ConcurrentQueue<IGameObject[]> _replacedArrays = new();
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

        /// <summary>
        /// Computes a 64-bit Morton code for a 2D coordinate.
        /// This improves cache locality by mapping 2D proximity to 1D address proximity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong GetMortonCode(long x, long y)
        {
            // Map to positive range for bit interleaving
            uint ux = (uint)(x + 0x80000000);
            uint uy = (uint)(y + 0x80000000);

            return Interleave(ux, uy);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Interleave(uint x, uint y)
        {
            return InterleaveBits(x) | (InterleaveBits(y) << 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong InterleaveBits(uint x)
        {
            if (Bmi2.X64.IsSupported)
            {
                return Bmi2.X64.ParallelBitDeposit(x, 0x5555555555555555);
            }

            if (AdvSimd.Arm64.IsSupported)
            {
                // ARM64 bit interleaving optimization
                return InterleaveBitsArm64(x);
            }

            ulong val = x;
            val = (val | (val << 16)) & 0x0000FFFF0000FFFF;
            val = (val | (val << 8)) & 0x00FF00FF00FF00FF;
            val = (val | (val << 4)) & 0x0F0F0F0F0F0F0F0F;
            val = (val | (val << 2)) & 0x3333333333333333;
            val = (val | (val << 1)) & 0x5555555555555555;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong InterleaveBitsArm64(uint x)
        {
            // ARM64 doesn't have PDEP, but we can use AdvSimd for vector bit manipulation
            // if we were interleaving multiple values. For a single uint, we use a specialized sequence.
            ulong val = x;
            val = (val | (val << 16)) & 0x0000FFFF0000FFFF;
            val = (val | (val << 8)) & 0x00FF00FF00FF00FF;
            val = (val | (val << 4)) & 0x0F0F0F0F0F0F0F0F;
            val = (val | (val << 2)) & 0x3333333333333333;
            val = (val | (val << 1)) & 0x5555555555555555;
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong GetCellKey(long x, long y)
        {
            return GetMortonCode(GetGridCoord(x), GetGridCoord(y));
        }

        private Cell GetOrCreateCell(Layer layer, ulong key)
        {
            if (layer.Grid.TryGetValue(key, out var cell)) return cell;

            var newCell = _cellPool.TryPop(out var pooled) ? pooled : new Cell();
            if (layer.Grid.TryAdd(key, newCell))
            {
                return newCell;
            }

            // Return to pool if we lost the race to create the cell
            _cellPool.Push(newCell);
            return layer.Grid[key];
        }

        public void Add(IGameObject obj)
        {
            var x = obj.X;
            var y = obj.Y;
            var z = obj.Z;
            var key = GetCellKey(x, y);
            var layer = GetLayer(z);

            if (obj.CurrentGridCellKey != null)
            {
                var old = obj.CurrentGridCellKey.Value;
                ulong oldKey = GetMortonCode(old.X, old.Y);
                if (oldKey == key && old.Z == z) return;

                var oldLayer = GetLayer(old.Z);
                var oldCell = GetOrCreateCell(oldLayer, oldKey);
                var cell = GetOrCreateCell(layer, key);

                // Consistent lock ordering to avoid deadlocks (Morton codes are stable unique ulongs within a layer)
                // We order first by layer then by Morton code within layer
                bool swap = old.Z > z || (old.Z == z && oldKey > key);
                var c1 = swap ? cell : oldCell;
                var c2 = swap ? oldCell : cell;

                bool lock1 = false, lock2 = false;
                try
                {
                    c1.Lock.Enter(ref lock1);
                    c2.Lock.Enter(ref lock2);
                    RemoveInternal(obj, oldLayer, oldCell);
                    AddInternal(obj, cell, new Vector3l(GetGridCoord(x), GetGridCoord(y), z));
                }
                finally
                {
                    if (lock2) c2.Lock.Exit(false);
                    if (lock1) c1.Lock.Exit(false);
                }
            }
            else
            {
                var cell = GetOrCreateCell(layer, key);
                bool lockTaken = false;
                try
                {
                    cell.Lock.Enter(ref lockTaken);
                    AddInternal(obj, cell, new Vector3l(GetGridCoord(x), GetGridCoord(y), z));
                }
                finally
                {
                    if (lockTaken) cell.Lock.Exit(false);
                }
            }
        }

        private void AddInternal(IGameObject obj, Cell cell, Vector3l key)
        {
            // Copy-On-Write to ensure lock-free reads during iteration
            int newCount = cell.Count + 1;
            int newSize = cell.Objects.Length;
            if (newCount > newSize)
            {
                newSize = newSize == 0 ? 4 : newSize * 2;
            }

            var newArray = ArrayPool<IGameObject>.Shared.Rent(newSize);
            var oldArray = cell.Objects;
            if (cell.Count > 0)
            {
                Array.Copy(oldArray, newArray, cell.Count);
            }

            if (oldArray.Length > 0)
            {
                // Enqueue old array for deferred return to avoid race with active enumerators
                _replacedArrays.Enqueue(oldArray);
            }

            obj.SpatialGridIndex = cell.Count;
            newArray[cell.Count] = obj;
            cell.Objects = newArray;
            cell.Count = newCount;
            obj.CurrentGridCellKey = key;
        }

        public void Remove(IGameObject obj)
        {
            if (obj.CurrentGridCellKey == null) return;
            var old = obj.CurrentGridCellKey.Value;
            ulong key = GetMortonCode(old.X, old.Y);
            var layer = GetLayer(old.Z);

            if (layer.Grid.TryGetValue(key, out var cell))
            {
                bool lockTaken = false;
                try
                {
                    cell.Lock.Enter(ref lockTaken);
                    RemoveInternal(obj, layer, cell);
                }
                finally
                {
                    if (lockTaken) cell.Lock.Exit(false);
                }
            }
        }

        private void RemoveInternal(IGameObject obj, Layer layer, Cell cell)
        {
            int index = obj.SpatialGridIndex;
            if (index == -1) return;

            int lastIndex = cell.Count - 1;
            var oldArray = cell.Objects;
            var newArray = ArrayPool<IGameObject>.Shared.Rent(oldArray.Length);

            if (lastIndex > 0)
            {
                Array.Copy(oldArray, newArray, cell.Count);
                if (index < lastIndex)
                {
                    var lastObj = newArray[lastIndex];
                    newArray[index] = lastObj;
                    lastObj.SpatialGridIndex = index;
                }
                newArray[lastIndex] = null!;
            }

            if (oldArray.Length > 0)
            {
                _replacedArrays.Enqueue(oldArray);
            }

            cell.Objects = newArray;
            cell.Count--;
            obj.SpatialGridIndex = -1;
            var oldKey = obj.CurrentGridCellKey;
            obj.CurrentGridCellKey = null;

            if (cell.Count == 0 && oldKey != null)
            {
                layer.EmptyCellKeys.Enqueue(GetMortonCode(oldKey.Value.X, oldKey.Value.Y));
            }
        }

        public void Update(IGameObject obj, long oldX, long oldY, long oldZ)
        {
            Add(obj);
        }

        public List<IGameObject> GetObjectsInBox(Box2l box, long z)
        {
            var results = new List<IGameObject>();
            GetObjectsInBox(box, z, results);
            return results;
        }

        public delegate void QueryCallback<TState>(IGameObject obj, ref TState state);

        public struct BoxEnumerator : IEnumerator<IGameObject>
        {
            private readonly SpatialGrid _grid;
            private readonly Layer? _layer;
            private readonly Box2l _box;
            private readonly long _endGX;
            private readonly long _endGY;
            private long _currentGX;
            private long _currentGY;
            private int _currentIndexInCell;
            private Cell? _currentCell;
            private bool _isCellContained;
            private bool _lockTaken;
            private IGameObject? _current;

            public BoxEnumerator(SpatialGrid grid, Box2l box, long z)
            {
                _grid = grid;
                _layer = grid._frozenLayers.TryGetValue(z, out var l) ? l : grid._layers.GetValueOrDefault(z);
                _box = box;
                _currentGX = grid.GetGridCoord(box.Left);
                _currentGY = grid.GetGridCoord(box.Bottom);
                _endGX = grid.GetGridCoord(box.Right);
                _endGY = grid.GetGridCoord(box.Top);
                _currentIndexInCell = -1;
                _currentCell = null;
                _lockTaken = false;
                _current = null;
            }

            public bool MoveNext()
            {
                while (true)
                {
                    if (_currentCell != null)
                    {
                        _currentIndexInCell++;
                        // Lock-free read of COW array
                        var objs = _currentCell.Objects;
                        if (_currentIndexInCell < _currentCell.Count && _currentIndexInCell < objs.Length)
                        {
                            _current = objs[_currentIndexInCell];
                            if (_current == null) continue;

                            // Optimization: Skip bounds check if cell is fully contained
                            if (_isCellContained || (_current.X >= _box.Left && _current.X <= _box.Right &&
                                                     _current.Y >= _box.Bottom && _current.Y <= _box.Top))
                            {
                                return true;
                            }
                            continue;
                        }
                    }

                    if (_currentGY > _endGY)
                    {
                        _currentGY = _grid.GetGridCoord(_box.Bottom);
                        _currentGX++;
                    }

                    if (_currentGX > _endGX) return false;
                    if (_layer == null) return false;

                    ulong key = GetMortonCode(_currentGX, _currentGY);

                    if (_layer.Grid.TryGetValue(key, out _currentCell))
                    {
                        if (Volatile.Read(ref _currentCell.Count) > 0)
                        {
                            _currentIndexInCell = -1;
                            // Check if this cell is fully contained within the query box
                            long cellX = _currentGX * _grid._cellSize;
                            long cellY = _currentGY * _grid._cellSize;
                            _isCellContained = cellX >= _box.Left && cellX + _grid._cellSize - 1 <= _box.Right &&
                                               cellY >= _box.Bottom && cellY + _grid._cellSize - 1 <= _box.Top;
                        }
                        else
                        {
                            _currentCell = null;
                        }
                    }
                    _currentGY++;
                }
            }

            public IGameObject Current => _current!;
            object System.Collections.IEnumerator.Current => Current;

            public void Reset() => throw new NotSupportedException();

            public void Dispose()
            {
                if (_lockTaken && _currentCell != null)
                {
                    _currentCell.Lock.Exit(false);
                    _lockTaken = false;
                }
            }

            public BoxEnumerator GetEnumerator() => this;
        }

        public BoxEnumerator GetEnumerator(Box2l box, long z) => new BoxEnumerator(this, box, z);

        public void QueryBox(Box2l box, long z, Action<IGameObject> callback)
        {
            var enumerator = new BoxEnumerator(this, box, z);
            try
            {
                while (enumerator.MoveNext())
                {
                    callback(enumerator.Current);
                }
            }
            finally
            {
                enumerator.Dispose();
            }
        }

        public void QueryBox<TState>(Box2l box, long z, ref TState state, QueryCallback<TState> callback)
        {
            var enumerator = new BoxEnumerator(this, box, z);
            try
            {
                while (enumerator.MoveNext())
                {
                    callback(enumerator.Current, ref state);
                }
            }
            finally
            {
                enumerator.Dispose();
            }
        }

        public void CleanupEmptyCells()
        {
            foreach (var layer in _layers.Values)
            {
                while (layer.EmptyCellKeys.TryDequeue(out var key))
                {
                    if (layer.Grid.TryGetValue(key, out var cell))
                    {
                        if (cell.Count == 0)
                        {
                            bool lockTaken = false;
                            try
                            {
                                cell.Lock.Enter(ref lockTaken);
                                if (cell.Count == 0)
                                {
                                    if (layer.Grid.TryRemove(key, out var removed))
                                    {
                                        removed.Clear();
                                        _cellPool.Push(removed);
                                    }
                                }
                            }
                            finally
                            {
                                if (lockTaken) cell.Lock.Exit(false);
                            }
                        }
                    }
                }
            }
        }

        public void GetObjectsInBox(Box2l box, long z, List<IGameObject> results)
        {
            results.Clear();
            GetObjectsInBox(box, z, (IList<IGameObject>)results);
        }

        /// <summary>
        /// Retrieves all objects in the given box and map level and adds them to the results list.
        /// This overload allows the caller to provide a pre-allocated list to avoid heap churn.
        /// </summary>
        public void GetObjectsInBox(Box2l box, long z, IList<IGameObject> results)
        {
            var enumerator = new BoxEnumerator(this, box, z);
            try
            {
                while (enumerator.MoveNext())
                {
                    results.Add(enumerator.Current);
                }
            }
            finally
            {
                enumerator.Dispose();
            }
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            Dispose();
            return base.StopAsync(cancellationToken);
        }

        public void Dispose()
        {
            foreach (var layer in _layers.Values)
            {
                foreach (var cell in layer.Grid.Values) cell.Dispose();
                layer.Grid.Clear();
            }
            _layers.Clear();
            _frozenLayers = FrozenDictionary<long, Layer>.Empty;
            _cellPool.Clear();
            GC.SuppressFinalize(this);
        }

        ~SpatialGrid()
        {
            Dispose();
        }
    }
