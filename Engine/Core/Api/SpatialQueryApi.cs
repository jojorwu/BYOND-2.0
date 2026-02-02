using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.Maths;
using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
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

        public List<GameObject> Range(int distance, int centerX, int centerY, int centerZ)
        {
            using (_gameState.ReadLock())
            {
                var distanceSquared = distance * distance;
                var box = new Box2i(centerX - distance, centerY - distance, centerX + distance, centerY + distance);
                var results = _gameState.SpatialGrid.GetObjectsInBox(box)
                    .Cast<GameObject>()
                    .Where(obj => obj.Z == centerZ && GetDistanceSquared(obj.X, obj.Y, obj.Z, centerX, centerY, centerZ) <= distanceSquared)
                    .ToList();
                return results;
            }
        }

        public List<GameObject> View(int distance, GameObject viewer)
        {
            using (_gameState.ReadLock())
            {
                var distanceSquared = distance * distance;
                var box = new Box2i(viewer.X - distance, viewer.Y - distance, viewer.X + distance, viewer.Y + distance);
                var results = _gameState.SpatialGrid.GetObjectsInBox(box)
                    .Cast<GameObject>()
                    .Where(obj => obj != viewer && obj.Z == viewer.Z && GetDistanceSquared(viewer, obj) <= distanceSquared && HasLineOfSight(viewer, obj))
                    .ToList();
                return results;
            }
        }

        private bool HasLineOfSight(GameObject from, GameObject to)
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
                            if (content.GetVariable("opacity").AsFloat() == 1.0f)
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

        private double GetDistanceSquared(GameObject a, GameObject b)
        {
            return GetDistanceSquared(a.X, a.Y, a.Z, b.X, b.Y, b.Z);
        }

        private double GetDistanceSquared(int x1, int y1, int z1, int x2, int y2, int z2)
        {
            var dx = x1 - x2;
            var dy = y1 - y2;
            var dz = z1 - z2;
            return dx * dx + dy * dy + dz * dz;
        }
    }
}
