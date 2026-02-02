using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Robust.Shared.Maths;

namespace Core.Api
{
    public class MapApi : IMapApi
    {
        private readonly IGameState _gameState;
        private readonly IMapLoader _mapLoader;
        private readonly IProject _project;
        private readonly IObjectTypeManager _objectTypeManager;

        public MapApi(IGameState gameState, IMapLoader mapLoader, IProject project, IObjectTypeManager objectTypeManager)
        {
            _gameState = gameState;
            _mapLoader = mapLoader;
            _project = project;
            _objectTypeManager = objectTypeManager;
        }

        public IMap? GetMap()
        {
            using (_gameState.ReadLock())
            {
                return _gameState.Map;
            }
        }

        public ITurf? GetTurf(int x, int y, int z)
        {
            using (_gameState.ReadLock())
            {
                return _gameState.Map?.GetTurf(x, y, z);
            }
        }

        public void SetTurf(int x, int y, int z, int turfId)
        {
            var turfType = _objectTypeManager.GetObjectType(turfId);
            if (turfType == null || !turfType.IsSubtypeOf(_objectTypeManager.GetTurfType()))
            {
                throw new System.ArgumentException($"Invalid or non-turf type ID: {turfId}", nameof(turfId));
            }

            using (_gameState.WriteLock())
            {
                _gameState.Map?.SetTurf(x, y, z, new Turf(turfType, x, y, z));
            }
        }

        public async Task<IMap?> LoadMapAsync(string filePath)
        {
            var safePath = PathSanitizer.Sanitize(_project, filePath, Constants.MapsRoot);
            var map = await _mapLoader.LoadMapAsync(safePath);
            using (_gameState.WriteLock())
            {
                _gameState.Map = map;
            }
            return map;
        }

        public void SetMap(IMap map)
        {
            using (_gameState.WriteLock())
            {
                _gameState.Map = map;
            }
        }

        public async Task SaveMapAsync(string filePath)
        {
            IMap? mapToSave;
            using (_gameState.ReadLock())
            {
                mapToSave = _gameState.Map;
            }

            if (mapToSave != null)
            {
                var safePath = PathSanitizer.Sanitize(_project, filePath, Constants.MapsRoot);
                await _mapLoader.SaveMapAsync(mapToSave, safePath);
            }
        }

        public IEnumerable<IGameObject> GetObjectsInRange(int x, int y, int z, int range)
        {
            return GetObjectsInRange(x, y, z, range, "/obj"); // Default to all objects
        }

        public IEnumerable<IGameObject> GetObjectsInRange(int x, int y, int z, int range, string typePath)
        {
            var targetType = _objectTypeManager.GetObjectType(typePath);
            if (targetType == null)
                return Enumerable.Empty<IGameObject>();

            var box = new Box2i(x - range, y - range, x + range, y + range);

            using (_gameState.ReadLock())
            {
                return _gameState.SpatialGrid.GetObjectsInBox(box)
                    .Where(obj => obj.Z == z && obj.ObjectType.IsSubtypeOf(targetType))
                    .ToList(); // ToList to execute the query inside the lock
            }
        }

        public IEnumerable<IGameObject> GetObjectsInArea(int x1, int y1, int x2, int y2, int z)
        {
            return GetObjectsInArea(x1, y1, x2, y2, z, "/obj"); // Default to all objects
        }

        public IEnumerable<IGameObject> GetObjectsInArea(int x1, int y1, int x2, int y2, int z, string typePath)
        {
            var targetType = _objectTypeManager.GetObjectType(typePath);
            if (targetType == null)
                return Enumerable.Empty<IGameObject>();

            var box = new Box2i(
                System.Math.Min(x1, x2),
                System.Math.Min(y1, y2),
                System.Math.Max(x1, x2),
                System.Math.Max(y1, y2)
            );

            using (_gameState.ReadLock())
            {
                return _gameState.SpatialGrid.GetObjectsInBox(box)
                    .Where(obj => obj.Z == z && obj.ObjectType.IsSubtypeOf(targetType))
                    .ToList(); // ToList to execute the query inside the lock
            }
        }
    }
}
