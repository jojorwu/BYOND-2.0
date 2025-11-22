using NUnit.Framework;
using Core;
using System;

namespace Core.Tests
{
    [TestFixture]
    public class ScriptingTests
    {
        [Test]
        public void ExecuteFile_WithNullPath_ShouldThrowArgumentNullException()
        {
            var scripting = new Scripting();
            Assert.Throws<ArgumentNullException>(() => scripting.ExecuteFile(null));
        }
    }
}
