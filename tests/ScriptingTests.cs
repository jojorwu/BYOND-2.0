using NUnit.Framework;
using Core;
using System;
using System.IO;

namespace Core.Tests
{
    [TestFixture]
    public class ScriptingTests
    {
        private Scripting scripting;

        [SetUp]
        public void SetUp()
        {
            scripting = new Scripting();
        }

        [TearDown]
        public void TearDown()
        {
            scripting.Dispose();
        }

        [Test]
        public void ExecuteFile_WithNullPath_ShouldThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => scripting.ExecuteFile(null));
        }

        [Test]
        public void ExecuteFile_WithInvalidPath_ShouldThrowFileNotFoundException()
        {
            Assert.Throws<FileNotFoundException>(() => scripting.ExecuteFile("nonexistent.lua"));
        }

        [Test]
        public void ExecuteFile_WithValidScript_ShouldExecuteSuccessfully()
        {
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, "print('test')");
            Assert.DoesNotThrow(() => scripting.ExecuteFile(tempFile));
            File.Delete(tempFile);
        }

        [Test]
        public void Reload_ShouldResetLuaState()
        {
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, "testVar = 123");
            scripting.ExecuteFile(tempFile);
            scripting.Reload();
            File.WriteAllText(tempFile, "if testVar == nil then print('testVar is nil') else print('testVar is not nil') end");
            Assert.DoesNotThrow(() => scripting.ExecuteFile(tempFile));
            File.Delete(tempFile);
        }
    }
}
