using NUnit.Framework;
using Core;
using System.IO;

namespace tests
{
    public class FileNotFound
    {
        private Scripting scripting;
        private GameApi gameApi;
        private GameState gameState;

        [SetUp]
        public void Setup()
        {
            gameState = new GameState();
            gameApi = new GameApi(gameState);
            scripting = new Scripting(gameApi);
        }

        [TearDown]
        public void Teardown()
        {
            scripting.Dispose();
        }

        [Test]
        public void ExecuteFile_NonExistentFile_ShouldThrowFileNotFoundException()
        {
            Assert.Throws<FileNotFoundException>(() => scripting.ExecuteFile("non-existent-file.lua"));
        }
    }
}
