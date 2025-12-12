using Shared;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;

namespace Shared
{
    public class GameState : IGameState
    {
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        public SpatialGrid SpatialGrid { get; } = new();
        public IMap? Map { get; set; }
        public Dictionary<int, GameObject> GameObjects { get; } = new Dictionary<int, GameObject>();

        public IDisposable ReadLock()
        {
            _lock.EnterReadLock();
            return new DisposableAction(() => _lock.ExitReadLock());
        }

        public IDisposable WriteLock()
        {
            _lock.EnterWriteLock();
            return new DisposableAction(() => _lock.ExitWriteLock());
        }

        public virtual string GetSnapshot()
        {
            using (ReadLock())
            {
                var snapshot = new
                {
                    Map,
                    GameObjects = GameObjects.Values.ToList() // Ensure we serialize the list of objects
                };
                return JsonSerializer.Serialize(snapshot);
            }
        }

        public string GetDeltaSnapshot()
        {
            using (WriteLock()) // Use write lock to modify IsDirty flags
            {
                var dirtyObjects = new List<IGameObject>();
                foreach (var obj in GameObjects.Values)
                {
                    if (obj.IsDirty)
                    {
                        dirtyObjects.Add(obj);
                        obj.IsDirty = false;
                    }
                }

                var dirtyTurfs = new List<ITurf>();
                if (Map != null)
                {
                    for (int z = 0; z < Map.Depth; z++)
                    {
                        for (int y = 0; y < Map.Height; y++)
                        {
                            for (int x = 0; x < Map.Width; x++)
                            {
                                var turf = Map.GetTurf(x, y, z);
                                if (turf != null && turf.IsDirty)
                                {
                                    dirtyTurfs.Add(turf);
                                    turf.IsDirty = false;
                                }
                            }
                        }
                    }
                }

                if (dirtyObjects.Count == 0 && dirtyTurfs.Count == 0)
                {
                    return string.Empty; // No changes
                }

                var delta = new
                {
                    UpdatedObjects = dirtyObjects,
                    UpdatedTurfs = dirtyTurfs
                };

                return JsonSerializer.Serialize(delta);
            }
        }

        public void Dispose()
        {
            _lock.Dispose();
        }

        public void Reset()
        {
            using (WriteLock())
            {
                GameObjects.Clear();
                SpatialGrid.Clear();
                Map = null;
            }
        }

        private sealed class DisposableAction : IDisposable
        {
            private readonly Action _action;

            public DisposableAction(Action action)
            {
                _action = action;
            }

            public void Dispose()
            {
                _action();
            }
        }
    }
}
