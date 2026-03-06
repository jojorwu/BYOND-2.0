using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.Maths;
using Shared;
using Shared.Api;

namespace Core.Api
{
    public class SpatialQueryApi : ISpatialQueryApi
    {
        private readonly IGameState _gameState;
        private readonly IObjectTypeManager _objectTypeManager;
        private readonly IMapApi _mapApi;

        public SpatialQueryApi(IGameState gameState, IObjectTypeManager objectTypeManager, IMapApi mapApi)
        {
            _gameState = gameState;
            _objectTypeManager = objectTypeManager;
            _mapApi = mapApi;
        }

        public bool CanMove(GameObject obj, long targetX, long targetY, long targetZ)
        {
             using (_gameState.ReadLock())
             {
                 var turf = _mapApi.GetTurf(targetX, targetY, targetZ);
                 if (turf == null) return false;

                 // Check density of other objects in the target turf
                 foreach (var content in turf.Contents)
                 {
                     if (content != obj && content is GameObject other && other.Density)
                     {
                         return false;
                     }
                 }
                 return true;
             }
        }

        public GameObject? Locate(string typePath, List<GameObject> container)
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
                    if (currentType.ParentName == null) break;
                    currentType = _objectTypeManager.GetObjectType(currentType.ParentName);
                }
            }

            return null;
        }

        private struct RangeState
        {
            public List<GameObject> Results;
            public int Distance;
            public long CenterX;
            public long CenterY;
            public long CenterZ;
        }

        public List<GameObject> Range(int distance, long centerX, long centerY, long centerZ)
        {
            var results = new List<GameObject>();
            var state = new RangeState { Results = results, Distance = distance, CenterX = centerX, CenterY = centerY, CenterZ = centerZ };

            using (_gameState.ReadLock())
            {
                var box = new Box2l(centerX - distance, centerY - distance, centerX + distance, centerY + distance);
                _gameState.SpatialGrid.QueryBox(box, ref state, static (IGameObject obj, ref RangeState s) =>
                {
                    if (obj.Z == s.CenterZ && obj is GameObject gameObj)
                    {
                        // BYOND range() uses Chebyshev distance: max(|x1-x2|, |y1-y2|)
                        if (Math.Max(Math.Abs(gameObj.X - s.CenterX), Math.Abs(gameObj.Y - s.CenterY)) <= s.Distance)
                        {
                            s.Results.Add(gameObj);
                        }
                    }
                });
            }
            return results;
        }

        private struct ViewState
        {
            public List<GameObject> Results;
            public int Distance;
            public GameObject Viewer;
            public SpatialQueryApi Api;
        }

        public List<GameObject> View(int distance, GameObject viewer)
        {
            var results = new List<GameObject>();
            var state = new ViewState { Results = results, Distance = distance, Viewer = viewer, Api = this };

            using (_gameState.ReadLock())
            {
                var box = new Box2l(viewer.X - distance, viewer.Y - distance, viewer.X + distance, viewer.Y + distance);
                _gameState.SpatialGrid.QueryBox(box, ref state, static (IGameObject obj, ref ViewState s) =>
                {
                    if (obj is GameObject gameObj && gameObj.Z == s.Viewer.Z)
                    {
                        if (Math.Max(Math.Abs(gameObj.X - s.Viewer.X), Math.Abs(gameObj.Y - s.Viewer.Y)) <= s.Distance)
                        {
                            if (gameObj == s.Viewer || s.Api.HasLineOfSight(s.Viewer, gameObj))
                            {
                                s.Results.Add(gameObj);
                            }
                        }
                    }
                });
            }
            return results;
        }

        private bool HasLineOfSight(GameObject from, GameObject to)
        {
            long x0 = from.X, y0 = from.Y;
            long x1 = to.X, y1 = to.Y;

            long dx = Math.Abs(x1 - x0);
            long sx = x0 < x1 ? 1 : -1;
            long dy = -Math.Abs(y1 - y0);
            long sy = y0 < y1 ? 1 : -1;
            long err = dx + dy;

            while (true)
            {
                var turf = _mapApi.GetTurf(x0, y0, from.Z);
                if (turf != null)
                {
                    foreach (var content in turf.Contents)
                    {
                        if (content != from && content != to && content is GameObject gameObj)
                        {
                            if (gameObj.Opacity == 1.0)
                            {
                                return false; // Blocked
                            }
                        }
                    }
                }

                if (x0 == x1 && y0 == y1) break;

                long e2 = 2 * err;
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

        private double GetDistanceSquared(GameObject a, GameObject b)
        {
            return GetDistanceSquared(a.X, a.Y, a.Z, b.X, b.Y, b.Z);
        }

        private double GetDistanceSquared(long x1, long y1, long z1, long x2, long y2, long z2)
        {
            var dx = x1 - x2;
            var dy = y1 - y2;
            var dz = z1 - z2;
            return (double)dx * dx + (double)dy * dy + (double)dz * dz;
        }
    }
}
