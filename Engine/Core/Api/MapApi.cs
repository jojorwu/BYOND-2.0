using Shared;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Robust.Shared.Maths;

namespace Core.Api
{
    public class MapApi : IMapApi
    {
        public string Name => "Map";
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

        public ITurf? GetTurf(long x, long y, long z)
        {
            using (_gameState.ReadLock())
            {
                return _gameState.Map?.GetTurf(x, y, z);
            }
        }

        public void SetTurf(long x, long y, long z, int turfId)
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

        public IEnumerable<IGameObject> GetObjectsInRange(long x, long y, long z, int range)
        {
            return GetObjectsInRange(x, y, z, range, "/obj"); // Default to all objects
        }

        public IEnumerable<IGameObject> GetObjectsInRange(long x, long y, long z, int range, string typePath)
        {
            return GetObjectsInArea(new Box3l(x - range, y - range, z, x + range, y + range, z), typePath);
        }

        public IEnumerable<IGameObject> GetObjectsInArea(Box3l box)
        {
            return GetObjectsInArea(box, "/obj");
        }

        public IEnumerable<IGameObject> GetObjectsInArea(Box3l box, string typePath)
        {
            var targetType = _objectTypeManager.GetObjectType(typePath);
            if (targetType == null)
                return Enumerable.Empty<IGameObject>();

            using (_gameState.ReadLock())
            {
                return _gameState.SpatialGrid.GetObjectsInBox(box)
                    .Where(obj => obj.ObjectType != null && obj.ObjectType.IsSubtypeOf(targetType))
                    .ToList(); // ToList to execute the query inside the lock
            }
        }

        public IEnumerable<IGameObject> GetObjectsInArea(long x1, long y1, long x2, long y2, long z)
        {
            return GetObjectsInArea(x1, y1, x2, y2, z, "/obj"); // Default to all objects
        }

        public IEnumerable<IGameObject> GetObjectsInArea(long x1, long y1, long x2, long y2, long z, string typePath)
        {
            var box = new Box3l(
                System.Math.Min(x1, x2),
                System.Math.Min(y1, y2),
                z,
                System.Math.Max(x1, x2),
                System.Math.Max(y1, y2),
                z
            );

            return GetObjectsInArea(box, typePath);
        }

        public bool CanMove(GameObject obj, long x, long y, long z)
        {
            using (_gameState.ReadLock())
            {
                var turf = _gameState.Map?.GetTurf(x, y, z);
                if (turf == null) return false;

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
    }
}
