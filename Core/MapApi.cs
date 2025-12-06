using System.Threading.Tasks;

namespace Core
{
    public class MapApi : IMapApi
    {
        private readonly GameState _gameState;
        private readonly MapLoader _mapLoader;
        private readonly IProject _project;

        public MapApi(GameState gameState, MapLoader mapLoader, IProject project)
        {
            _gameState = gameState;
            _mapLoader = mapLoader;
            _project = project;
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
            using (_gameState.WriteLock())
            {
                _gameState.Map?.SetTurf(x, y, z, new Turf(turfId));
            }
        }

        public async Task<IMap?> LoadMapAsync(string filePath)
        {
            var safePath = SanitizePath(filePath, Constants.MapsRoot);
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
                var safePath = SanitizePath(filePath, Constants.MapsRoot);
                await _mapLoader.SaveMapAsync(mapToSave, safePath);
            }
        }

        private string SanitizePath(string userProvidedPath, string expectedRootFolder)
        {
            // Get the full path of the project's root for the given type (e.g., /tmp/proj/scripts)
            var fullRootPath = System.IO.Path.GetFullPath(_project.GetFullPath(expectedRootFolder));

            // Get the full path of the user-provided file relative to the project root
            var fullUserPath = System.IO.Path.GetFullPath(_project.GetFullPath(userProvidedPath));


            if (!fullUserPath.StartsWith(fullRootPath))
            {
                throw new System.Security.SecurityException("Access to path is denied.");
            }
            return fullUserPath;
        }
    }
}
