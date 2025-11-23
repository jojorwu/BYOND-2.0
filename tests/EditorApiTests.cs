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
        private const string TestProjectDir = "TestProject";
        private Project _project = null!;
        private MapLoader _mapLoader = null!;

        [SetUp]
        public void SetUp()
        {
            Directory.CreateDirectory(TestProjectDir);
            _project = new Project(TestProjectDir);
            _gameState = new GameState();
            _objectTypeManager = new ObjectTypeManager(_project);
            _mapLoader = new MapLoader(_objectTypeManager, _project);
            _gameApi = new GameApi(_gameState, _objectTypeManager, _mapLoader, _project);
            _editorApi = new EditorApi(_project);
            _scripting = new Scripting(_gameApi, _editorApi);
        }

        [TearDown]
        public void TearDown()
        {
            _scripting.Dispose();
            if (Directory.Exists(TestProjectDir))
            {
                Directory.Delete(TestProjectDir, true);
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
