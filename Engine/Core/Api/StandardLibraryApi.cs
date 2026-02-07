using Shared;
using System;
using System.Collections.Generic;
using System.Threading;
using Shared.Api;

namespace Core.Api
{
    public class StandardLibraryApi : IStandardLibraryApi
    {
        private readonly ISpatialQueryApi _spatialQueryApi;
        private readonly IMapApi _mapApi;

        public StandardLibraryApi(ISpatialQueryApi spatialQueryApi, IMapApi mapApi)
        {
            _spatialQueryApi = spatialQueryApi;
            _mapApi = mapApi;
        }

        public GameObject? Locate(string typePath, List<GameObject> container)
        {
            return _spatialQueryApi.Locate(typePath, container);
        }

        public void Sleep(int milliseconds)
        {
            Console.WriteLine($"[Warning] Game:Sleep({milliseconds}) is a blocking operation and will freeze the server thread. Use with caution.");
            Thread.Sleep(milliseconds);
        }

        public List<GameObject> Range(int distance, int centerX, int centerY, int centerZ)
        {
            return _spatialQueryApi.Range(distance, centerX, centerY, centerZ);
        }

        public List<GameObject> View(int distance, GameObject viewer)
        {
            return _spatialQueryApi.View(distance, viewer);
        }

        public int Step(GameObject obj, int dir, int speed)
        {
            int dx = 0, dy = 0;
            if ((dir & 1) != 0) dy++; // NORTH
            if ((dir & 2) != 0) dy--; // SOUTH
            if ((dir & 4) != 0) dx++; // EAST
            if ((dir & 8) != 0) dx--; // WEST

            int newX = obj.X + dx;
            int newY = obj.Y + dy;

            // Simple collision check: check if the target turf is walkable
            var turf = _mapApi.GetTurf(newX, newY, obj.Z);
            if (turf != null)
            {
                // In a real engine, we'd check density, but for now let's just move
                obj.X = newX;
                obj.Y = newY;
                return 1;
            }

            return 0;
        }

        public int StepTo(GameObject obj, GameObject target, int minDist, int speed)
        {
            if (obj.Z != target.Z) return 0;

            var dx = target.X - obj.X;
            var dy = target.Y - obj.Y;

            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) <= minDist) return 0;

            int stepX = Math.Sign(dx);
            int stepY = Math.Sign(dy);

            return Step(obj, GetDir(stepX, stepY), speed);
        }

        private int GetDir(int dx, int dy)
        {
            int dir = 0;
            if (dy > 0) dir |= 1; // NORTH
            else if (dy < 0) dir |= 2; // SOUTH
            if (dx > 0) dir |= 4; // EAST
            else if (dx < 0) dir |= 8; // WEST
            return dir;
        }
    }
}
