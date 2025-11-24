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

        [SetUp]
        public void SetUp()
        {
            _gameState = new GameState();
            _objectTypeManager = new ObjectTypeManager();
            _mapLoader = new MapLoader(_objectTypeManager);
            _gameApi = new GameApi(_gameState, _objectTypeManager, _mapLoader);
            _editorApi = new EditorApi();
            _scripting = new Scripting(_gameApi, _editorApi);
        }

        [TearDown]
        public void TearDown()
        {
            _scripting.Dispose();
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
