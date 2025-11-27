using NUnit.Framework;
using Core;

namespace Core.Tests
{
    [TestFixture]
    public class EditorApiTests
    {
        private Scripting _scripting = null!;
        private GameApi _gameApi = null!;
        private GameState _gameState = null!;
        private EditorApi _editorApi = null!;
        private ObjectTypeManager _objectTypeManager = null!;
        private MapLoader _mapLoader = null!;
        private Project _project = null!;
        private string _projectPath = null!;

        [SetUp]
        public void SetUp()
        {
            _projectPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_projectPath);
            _project = new Project(_projectPath);

            _gameState = new GameState();
            _objectTypeManager = new ObjectTypeManager();
            _mapLoader = new MapLoader(_objectTypeManager);
            _gameApi = new GameApi(_project, _gameState, _objectTypeManager, _mapLoader);
            _editorApi = new EditorApi();
            _scripting = new Scripting(_gameApi, _editorApi);
        }

        [TearDown]
        public void TearDown()
        {
            _scripting.Dispose();
            if (Directory.Exists(_projectPath))
            {
                Directory.Delete(_projectPath, true);
            }
        }

        [Test]
        public void EditorApi_IsAccessibleFromLua()
        {
            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                _scripting.ExecuteString("print(Editor)");
                _scripting.ExecuteString("print(Editor.State)");
                _scripting.ExecuteString("print(Editor.Selection)");
                _scripting.ExecuteString("print(Editor.Assets)");
            });
        }
    }
}
