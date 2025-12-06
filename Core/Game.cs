namespace Core
{
    public class Game : IGame
    {
        private Project? _project;
        private GameState? _gameState;
        private ObjectTypeManager? _objectTypeManager;
        private MapLoader? _mapLoader;
        private GameApi? _gameApi;
        private OpenDreamCompilerService? _compilerService;
        private DmmService? _dmmService;
        public IGameApi Api => _gameApi!;
        public IProject Project => _project!;
        public IObjectTypeManager ObjectTypeManager => _objectTypeManager!;
        public IDmmService DmmService => _dmmService!;

        public void LoadProject(string projectPath)
        {
            _project = new Project(projectPath);
            _gameState = new GameState();
            _objectTypeManager = new ObjectTypeManager();
            _mapLoader = new MapLoader(_objectTypeManager);
            var mapApi = new MapApi(_gameState, _mapLoader, _project);
            var objectApi = new ObjectApi(_gameState, _objectTypeManager, mapApi);
            var scriptApi = new ScriptApi(_project);
            var standardLibraryApi = new StandardLibraryApi(_gameState, _objectTypeManager, mapApi);
            _gameApi = new GameApi(mapApi, objectApi, scriptApi, standardLibraryApi);
            _compilerService = new OpenDreamCompilerService(_project);
            _dmmService = new DmmService(_objectTypeManager, _project);
            var wall = new ObjectType("wall");
            wall.DefaultProperties["SpritePath"] = "assets/wall.png";
            _objectTypeManager.RegisterObjectType(wall);
            var floor = new ObjectType("floor");
            floor.DefaultProperties["SpritePath"] = "assets/floor.png";
            _objectTypeManager.RegisterObjectType(floor);
        }
    }
}
