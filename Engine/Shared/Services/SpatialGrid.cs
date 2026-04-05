using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;
using System;
using System.Collections.Generic;
using System.Threading;
using Robust.Shared.Maths;
using System.Collections.Concurrent;
using System.Buffers;
using Shared.Interfaces;
using Microsoft.Extensions.Logging;
using Shared.Services;
using Shared.Attributes;

namespace Shared;

[EngineService]
public class SpatialGrid : EngineService, IDisposable, IShrinkable
{
    private class Cell
    {
        public volatile IGameObject[] Objects = Array.Empty<IGameObject>();
        public volatile long[] Xs = Array.Empty<long>();
        public volatile long[] Ys = Array.Empty<long>();
        public volatile long[] Zs = Array.Empty<long>();
        public int Count;
        public long Version;
        public int Readers;

        public void Reset()
        {
            Objects = Array.Empty<IGameObject>();
            Xs = Array.Empty<long>();
            Ys = Array.Empty<long>();
            Zs = Array.Empty<long>();
            Count = 0;
            Version = 0;
            Readers = 0;
        }
    }

    private class GridChunk
    {
        public readonly Cell?[] Cells = new Cell[32 * 32];
        public int ActiveCells;
    }

    private class LevelLayer
    {
        public readonly ConcurrentDictionary<ulong, GridChunk> Chunks = new();
    }

    private readonly ConcurrentDictionary<long, LevelLayer> _levels = new();
    private readonly ConcurrentQueue<ulong> _emptyCellKeys = new();
    private readonly ConcurrentQueue<(long ReleaseTimestamp, IGameObject[] Array)> _replacedArrays = new();
    private readonly ConcurrentQueue<(long ReleaseTimestamp, long[] Array)> _replacedCoordArrays = new();
    private readonly ConcurrentStack<Cell> _cellPool = new();
    private readonly TimeProvider _timeProvider;
    private readonly int _cellSize;
    private readonly ILogger<SpatialGrid> _logger;
    private readonly IDiagnosticBus _diagnosticBus;
    private long _version;
    private long _totalQueries;
    private long _objectsMoved;

    public long Version => Volatile.Read(ref _version);

    public SpatialGrid(ILogger<SpatialGrid> logger, TimeProvider timeProvider, IDiagnosticBus diagnosticBus, int cellSize = 32)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        _diagnosticBus = diagnosticBus;
        _cellSize = cellSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long GetGridCoord(long val) => (val >= 0) ? (val / _cellSize) : ((val - _cellSize + 1) / _cellSize);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (long ChunkX, long ChunkY, int CellIdx) GetChunkCoords(long gx, long gy)
    {
        long cx = gx >> 5;
        long cy = gy >> 5;
        int ci = (int)((gx & 31) | ((gy & 31) << 5));
        return (cx, cy, ci);
    }

    private Cell GetOrCreateCell(long x, long y, long z)
    {
        long gx = GetGridCoord(x);
        long gy = GetGridCoord(y);
        return GetOrCreateCellFromGrid(gx, gy, z);
    }

    private Cell GetOrCreateCellFromGrid(long gx, long gy, long z)
    {
        var (cx, cy, ci) = GetChunkCoords(gx, gy);

        var layer = _levels.GetOrAdd(z, _ => new LevelLayer());
        ulong chunkKey = (ulong)((uint)cx) | ((ulong)((uint)cy) << 32);
        var chunk = layer.Chunks.GetOrAdd(chunkKey, _ => new GridChunk());

        if (chunk.Cells[ci] == null)
        {
            lock (chunk)
            {
                if (chunk.Cells[ci] == null)
                {
                    if (!_cellPool.TryPop(out var pooled)) pooled = new Cell();
                    chunk.Cells[ci] = pooled;
                    chunk.ActiveCells++;
                }
            }
        }
        return chunk.Cells[ci]!;
    }

    private Cell? GetCell(long gx, long gy, long z)
    {
        if (!_levels.TryGetValue(z, out var layer)) return null;
        var (cx, cy, ci) = GetChunkCoords(gx, gy);
        ulong chunkKey = (ulong)((uint)cx) | ((ulong)((uint)cy) << 32);
        if (!layer.Chunks.TryGetValue(chunkKey, out var chunk)) return null;
        return chunk.Cells[ci];
    }

    public void Add(IGameObject obj)
    {
        var pos = obj.Position;
        long gx = GetGridCoord(pos.X);
        long gy = GetGridCoord(pos.Y);

        if (obj.CurrentGridCellKey != null)
        {
            var oldKey = obj.CurrentGridCellKey.Value;
            if (gx == oldKey.X && gy == oldKey.Y && pos.Z == oldKey.Z) return;

            Interlocked.Increment(ref _objectsMoved);
            var cell = GetOrCreateCellFromGrid(gx, gy, pos.Z);
            var oldCellCopy = GetOrCreateCellFromGrid(oldKey.X, oldKey.Y, oldKey.Z);

            bool lock1 = false, lock2 = false;
            try
            {
                object lockFirst = oldCellCopy, lockSecond = cell;
                if (System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(oldCellCopy) > System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(cell))
                {
                    lockFirst = cell; lockSecond = oldCellCopy;
                }

                Monitor.Enter(lockFirst, ref lock1);
                Monitor.Enter(lockSecond, ref lock2);

                RemoveInternal(obj, oldCellCopy);
                AddInternal(obj, cell, new Vector3l(gx, gy, pos.Z));
            }
            finally
            {
                if (lock2) Monitor.Exit(cell);
                if (lock1) Monitor.Exit(oldCellCopy);
            }
        }
        else
        {
            var cell = GetOrCreateCellFromGrid(gx, gy, pos.Z);
            lock (cell)
            {
                AddInternal(obj, cell, new Vector3l(gx, gy, pos.Z));
            }
        }
    }

    private void AddInternal(IGameObject obj, Cell cell, Vector3l key)
    {
        int newCount = cell.Count + 1;
        if (Volatile.Read(ref cell.Readers) == 0 && newCount <= cell.Objects.Length)
        {
            obj.SpatialGridIndex = cell.Count;
            cell.Objects[cell.Count] = obj;
            cell.Xs[cell.Count] = obj.X;
            cell.Ys[cell.Count] = obj.Y;
            cell.Zs[cell.Count] = obj.Z;
        }
        else
        {
            var oldArray = cell.Objects;
            int targetSize = Math.Max(4, oldArray.Length);
            if (newCount > targetSize) targetSize *= 2;

            var newArray = ArrayPool<IGameObject>.Shared.Rent(targetSize);
            var newX = ArrayPool<long>.Shared.Rent(targetSize);
            var newY = ArrayPool<long>.Shared.Rent(targetSize);
            var newZ = ArrayPool<long>.Shared.Rent(targetSize);

            if (cell.Count > 0)
            {
                Array.Copy(oldArray, newArray, cell.Count);
                Array.Copy(cell.Xs, newX, cell.Count);
                Array.Copy(cell.Ys, newY, cell.Count);
                Array.Copy(cell.Zs, newZ, cell.Count);
            }

            if (oldArray.Length > 0)
            {
                EnqueueForRelease(oldArray);
                EnqueueForRelease(cell.Xs);
                EnqueueForRelease(cell.Ys);
                EnqueueForRelease(cell.Zs);
            }

            obj.SpatialGridIndex = cell.Count;
            newArray[cell.Count] = obj;
            newX[cell.Count] = obj.X;
            newY[cell.Count] = obj.Y;
            newZ[cell.Count] = obj.Z;

            cell.Objects = newArray;
            cell.Xs = newX;
            cell.Ys = newY;
            cell.Zs = newZ;
        }

        cell.Count = newCount;
        cell.Version++;
        Interlocked.Increment(ref _version);
        obj.CurrentGridCellKey = key;
    }

    public void Remove(IGameObject obj)
    {
        if (obj.CurrentGridCellKey == null) return;
        var key = obj.CurrentGridCellKey.Value;
        var cell = GetCell(key.X, key.Y, key.Z);
        if (cell != null)
        {
            lock (cell)
            {
                RemoveInternal(obj, cell);
            }
        }
    }

    private void RemoveInternal(IGameObject obj, Cell cell)
    {
        int index = obj.SpatialGridIndex;
        if (index == -1) return;

        int lastIndex = cell.Count - 1;
        if (Volatile.Read(ref cell.Readers) == 0)
        {
            if (index < lastIndex)
            {
                var lastObj = cell.Objects[lastIndex];
                cell.Objects[index] = lastObj;
                cell.Xs[index] = cell.Xs[lastIndex];
                cell.Ys[index] = cell.Ys[lastIndex];
                cell.Zs[index] = cell.Zs[lastIndex];
                lastObj.SpatialGridIndex = index;
            }
            cell.Objects[lastIndex] = null!;
        }
        else
        {
            var oldObjects = cell.Objects;
            var newArray = ArrayPool<IGameObject>.Shared.Rent(oldObjects.Length);
            var newX = ArrayPool<long>.Shared.Rent(cell.Xs.Length);
            var newY = ArrayPool<long>.Shared.Rent(cell.Ys.Length);
            var newZ = ArrayPool<long>.Shared.Rent(cell.Zs.Length);

            Array.Clear(newArray, 0, newArray.Length);

            if (lastIndex > 0)
            {
                Array.Copy(oldObjects, newArray, cell.Count);
                Array.Copy(cell.Xs, newX, cell.Count);
                Array.Copy(cell.Ys, newY, cell.Count);
                Array.Copy(cell.Zs, newZ, cell.Count);

                if (index < lastIndex)
                {
                    var lastObj = newArray[lastIndex];
                    newArray[index] = lastObj;
                    newX[index] = newX[lastIndex];
                    newY[index] = newY[lastIndex];
                    newZ[index] = newZ[lastIndex];
                    lastObj.SpatialGridIndex = index;
                }
                newArray[lastIndex] = null!;
            }

            EnqueueForRelease(oldObjects);
            EnqueueForRelease(cell.Xs);
            EnqueueForRelease(cell.Ys);
            EnqueueForRelease(cell.Zs);

            cell.Objects = newArray;
            cell.Xs = newX;
            cell.Ys = newY;
            cell.Zs = newZ;
        }

        cell.Count--;
        cell.Version++;
        Interlocked.Increment(ref _version);
        obj.SpatialGridIndex = -1;
        var oldKey = obj.CurrentGridCellKey;
        obj.CurrentGridCellKey = null;

        if (cell.Count == 0 && oldKey != null)
        {
            var key = oldKey.Value;
            _emptyCellKeys.Enqueue((ulong)(uint)key.X | ((ulong)(uint)key.Y << 32) | ((ulong)(ushort)key.Z << 48));
        }
    }

    public void Update(IGameObject obj, long oldX, long oldY) => Add(obj);

    public List<IGameObject> GetObjectsInBox(Box3l box)
    {
        var results = new List<IGameObject>();
        GetObjectsInBox(box, results);
        return results;
    }

    public void GetObjectsInBox(Box3l box, List<IGameObject> results)
    {
        results.Clear();
        var enumerator = new BoxEnumerator(this, box);
        while (enumerator.MoveNext()) results.Add(enumerator.Current);
    }

    public void QueryBox(Box3l box, Action<IGameObject> callback)
    {
        var enumerator = new BoxEnumerator(this, box);
        while (enumerator.MoveNext()) callback(enumerator.Current);
    }

    public delegate void QueryCallback<TState>(IGameObject obj, ref TState state);
    public void QueryBox<TState>(Box3l box, ref TState state, QueryCallback<TState> callback)
    {
        var enumerator = new BoxEnumerator(this, box);
        while (enumerator.MoveNext()) callback(enumerator.Current, ref state);
    }

    public struct BoxEnumerator : IEnumerator<IGameObject>
    {
        private readonly SpatialGrid _grid;
        private readonly Box3l _box;
        private readonly long _endGX, _endGY, _endGZ;
        private long _currentGX, _currentGY, _currentGZ;
        private int _currentIndexInCell;
        private Cell? _currentCell;
        private IGameObject? _current;

        public BoxEnumerator(SpatialGrid grid, Box3l box)
        {
            _grid = grid;
            Interlocked.Increment(ref _grid._totalQueries);
            _box = box;
            _currentGX = grid.GetGridCoord(box.Left);
            _currentGY = grid.GetGridCoord(box.Bottom);
            _currentGZ = box.Back;
            _endGX = grid.GetGridCoord(box.Right);
            _endGY = grid.GetGridCoord(box.Top);
            _endGZ = box.Front;
            _currentIndexInCell = -1;

            _currentCell = _grid.GetCell(_currentGX, _currentGY, _currentGZ);
            if (_currentCell != null)
            {
                if (Volatile.Read(ref _currentCell.Count) > 0)
                    Interlocked.Increment(ref _currentCell.Readers);
                else
                    _currentCell = null;
            }
            _current = null;
        }

        public bool MoveNext()
        {
            while (true)
            {
                if (_currentCell != null)
                {
                    var objs = _currentCell.Objects;
                    var xs = _currentCell.Xs;
                    var ys = _currentCell.Ys;
                    var zs = _currentCell.Zs;
                    int count = _currentCell.Count;

                    while (++_currentIndexInCell < count)
                    {
                        if (xs[_currentIndexInCell] >= _box.Left && xs[_currentIndexInCell] <= _box.Right &&
                            ys[_currentIndexInCell] >= _box.Bottom && ys[_currentIndexInCell] <= _box.Top &&
                            zs[_currentIndexInCell] >= _box.Back && zs[_currentIndexInCell] <= _box.Front)
                        {
                            _current = objs[_currentIndexInCell];
                            return true;
                        }
                    }
                }

                _currentIndexInCell = -1;
                if (++_currentGX > _endGX)
                {
                    _currentGX = _grid.GetGridCoord(_box.Left);
                    if (++_currentGY > _endGY)
                    {
                        _currentGY = _grid.GetGridCoord(_box.Bottom);
                        if (++_currentGZ > _endGZ) return false;
                    }
                }

                _currentCell = _grid.GetCell(_currentGX, _currentGY, _currentGZ);
                if (_currentCell != null)
                {
                    if (Volatile.Read(ref _currentCell.Count) > 0)
                        Interlocked.Increment(ref _currentCell.Readers);
                    else
                        _currentCell = null;
                }
            }
        }

        public IGameObject Current => _current!;
        object System.Collections.IEnumerator.Current => Current;
        public void Reset() => throw new NotSupportedException();
        public void Dispose() { if (_currentCell != null) Interlocked.Decrement(ref _currentCell.Readers); }
    }

    public BoxEnumerator GetEnumerator(Box3l box) => new(this, box);

    public void Shrink()
    {
        CleanupEmptyCells();
        long now = _timeProvider.GetTimestamp();
        while (_replacedArrays.TryPeek(out var entry) && now >= entry.ReleaseTimestamp)
        {
            if (_replacedArrays.TryDequeue(out entry)) ArrayPool<IGameObject>.Shared.Return(entry.Array, true);
        }
        while (_replacedCoordArrays.TryPeek(out var entry) && now >= entry.ReleaseTimestamp)
        {
            if (_replacedCoordArrays.TryDequeue(out entry)) ArrayPool<long>.Shared.Return(entry.Array, true);
        }
    }

    public void CleanupEmptyCells()
    {
        int processed = 0;
        while (processed++ < 128 && _emptyCellKeys.TryDequeue(out var key))
        {
            long z = (long)(ushort)(key >> 48);
            long gx = (long)(int)(uint)key;
            long gy = (long)(int)(uint)(key >> 32);

            if (_levels.TryGetValue(z, out var layer))
            {
                var (cx, cy, ci) = GetChunkCoords(gx, gy);
                ulong chunkKey = (ulong)((uint)cx) | ((ulong)((uint)cy) << 32);
                if (layer.Chunks.TryGetValue(chunkKey, out var chunk))
                {
                    var cell = chunk.Cells[ci];
                    if (cell != null && Volatile.Read(ref cell.Count) == 0)
                    {
                        lock (cell)
                        {
                            if (cell.Count == 0)
                            {
                                EnqueueForRelease(cell.Objects);
                                EnqueueForRelease(cell.Xs);
                                EnqueueForRelease(cell.Ys);
                                EnqueueForRelease(cell.Zs);
                                cell.Reset();
                                _cellPool.Push(cell);
                                chunk.Cells[ci] = null;
                                if (--chunk.ActiveCells == 0) layer.Chunks.TryRemove(chunkKey, out _);
                            }
                        }
                    }
                }
            }
        }
    }

    private void EnqueueForRelease(IGameObject[] array)
    {
        if (array.Length > 0) _replacedArrays.Enqueue((_timeProvider.GetTimestamp() + _timeProvider.TimestampFrequency, array));
    }
    private void EnqueueForRelease(long[] array)
    {
        if (array.Length > 0) _replacedCoordArrays.Enqueue((_timeProvider.GetTimestamp() + _timeProvider.TimestampFrequency, array));
    }

    public void Dispose()
    {
        foreach (var layer in _levels.Values) layer.Chunks.Clear();
        _levels.Clear();
    }
}
