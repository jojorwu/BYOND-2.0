using Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Core
{
    public class StandardLibraryApi : IStandardLibraryApi
    {
        private readonly IGameState _gameState;
        private readonly IObjectTypeManager _objectTypeManager;
        private readonly IMapApi _mapApi;
        private readonly IRestartService _restartService;

        public StandardLibraryApi(IGameState gameState, IObjectTypeManager objectTypeManager, IMapApi mapApi, IRestartService restartService)
        {
            _gameState = gameState;
            _objectTypeManager = objectTypeManager;
            _mapApi = mapApi;
            _restartService = restartService;
        }

        public void Restart()
        {
            _restartService.RequestRestart();
        }

        public IGameObject? Locate(string typePath, List<IGameObject> container)
        {
            var targetType = _objectTypeManager.GetObjectType(typePath);
            if (targetType == null) return null;

            foreach (var obj in container)
            {
                var currentType = obj.ObjectType;
                while (currentType != null)
                {
                    if (currentType == targetType)
                    {
                        return obj;
                    }
                    if (currentType.Parent == null) break;
                    currentType = currentType.Parent;
                }
            }

            return null;
        }

        public List<IGameObject> Range(float distance, int centerX, int centerY, int centerZ)
        {
            using (_gameState.ReadLock())
            {
                var candidates = _gameState.SpatialGrid.Query(centerX, centerY, (int)Math.Ceiling(distance));

                var results = new List<IGameObject>();
                foreach (var obj in candidates)
                {
                    if (GetDistance(obj.X, obj.Y, obj.Z, centerX, centerY, centerZ) <= distance)
                    {
                        results.Add(obj);
                    }
                }
                return results;
            }
        }

        public List<IGameObject> View(int distance, IGameObject viewer)
        {
            using (_gameState.ReadLock())
            {
                var candidates = _gameState.SpatialGrid.Query(viewer.X, viewer.Y, distance);

                var results = new List<IGameObject>();
                foreach (var obj in candidates)
                {
                    if (obj == viewer) continue;

                    if (GetDistance(viewer, obj) <= distance)
                    {
                        if (HasLineOfSight(viewer, obj))
                        {
                            results.Add(obj);
                        }
                    }
                }
                return results;
            }
        }

        private bool HasLineOfSight(IGameObject from, IGameObject to)
        {
            int x0 = from.X, y0 = from.Y;
            int x1 = to.X, y1 = to.Y;

            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                var turf = _mapApi.GetTurf(x0, y0, from.Z);
                if (turf != null)
                {
                    foreach (var content in turf.Contents)
                    {
                        if (content != from && content != to)
                        {
                            if (content.GetProperty<int>("opacity") == 1)
                            {
                                return false; // Blocked
                            }
                        }
                    }
                }

                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }

            return true; // No obstructions
        }

        private double GetDistance(IGameObject a, IGameObject b)
        {
            return GetDistance(a.X, a.Y, a.Z, b.X, b.Y, b.Z);
        }

        private double GetDistance(int x1, int y1, int z1, int x2, int y2, int z2)
        {
            var dx = x1 - x2;
            var dy = y1 - y2;
            var dz = z1 - z2;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
