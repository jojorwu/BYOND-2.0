using System.Threading.Tasks;

namespace Core
{
    public class MapApi : IMapApi
    {
        private readonly GameState _gameState;
        private readonly MapLoader _mapLoader;
        private readonly Project _project;

        public MapApi(GameState gameState, MapLoader mapLoader, Project project)
        {
            _gameState = gameState;
            _mapLoader = mapLoader;
            _project = project;
        }

        public Map? GetMap()
        {
            return _gameState.Map;
        }

        public Turf? GetTurf(int x, int y, int z)
        {
            return _gameState.Map?.GetTurf(x, y, z);
        }

        public void SetTurf(int x, int y, int z, int turfId)
        {
            _gameState.Map?.SetTurf(x, y, z, new Turf(turfId));
        }

        public async Task LoadMapAsync(string filePath)
        {
            var safePath = SanitizePath(filePath, Constants.MapsRoot);
            _gameState.Map = await _mapLoader.LoadMapAsync(safePath);
        }

        public void SetMap(Map map)
        {
            _gameState.Map = map;
        }

        public async Task SaveMapAsync(string filePath)
        {
            if (_gameState.Map != null)
            {
                var safePath = SanitizePath(filePath, Constants.MapsRoot);
                await _mapLoader.SaveMapAsync(_gameState.Map, safePath);
            }
        }

        private string SanitizePath(string userProvidedPath, string expectedRootFolder)
        {
            var fullRootPath = System.IO.Path.GetFullPath(_project.GetFullPath(expectedRootFolder));
            var fullUserPath = System.IO.Path.GetFullPath(_project.GetFullPath(userProvidedPath));

            if (!fullUserPath.StartsWith(fullRootPath))
            {
                throw new System.Security.SecurityException("Access to path is denied.");
            }
            return fullUserPath;
        }
    }
}
