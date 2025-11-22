using NUnit.Framework;
using Core;
using System.IO;

namespace tests
{
    public class FileNotFound
    {
        private Scripting scripting;

        [SetUp]
        public void Setup()
        {
            scripting = new Scripting();
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
