using NUnit.Framework;
using Core;

namespace Core.Tests
{
    [TestFixture]
    public class EditorApiTests
    {
        private Scripting scripting;
        private GameApi gameApi;
        private GameState gameState;
        private EditorApi editorApi;

        [SetUp]
        public void SetUp()
        {
            gameState = new GameState();
            gameApi = new GameApi(gameState);
            editorApi = new EditorApi();
            scripting = new Scripting(gameApi, editorApi);
        }

        [TearDown]
        public void TearDown()
        {
            scripting.Dispose();
        }

        [Test]
        public void EditorApi_IsAccessibleFromLua()
        {
            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                scripting.ExecuteString("print(Editor)");
                scripting.ExecuteString("print(Editor.State)");
                scripting.ExecuteString("print(Editor.Selection)");
                scripting.ExecuteString("print(Editor.Assets)");
            });
        }
    }
}
