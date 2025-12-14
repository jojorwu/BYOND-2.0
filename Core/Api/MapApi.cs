using Shared;
using System.Threading.Tasks;

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
            if (turfType == null || !IsTurfType(turfType))
            {
                // Maybe just log a warning? For now, an exception is better to signal API misuse.
                throw new System.ArgumentException($"Invalid or non-turf type ID: {turfId}", nameof(turfId));
            }

            using (_gameState.WriteLock())
            {
                _gameState.Map?.SetTurf(x, y, z, new Turf(turfId));
            }
        }

        private bool IsTurfType(ObjectType? type)
        {
            var current = type;
            while (current != null)
            {
                if (current.Name == "/turf")
                {
                    return true;
                }
                current = current.Parent;
            }
            return false;
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
    }
}
