using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using NUnit.Framework;

namespace Core.Tests
{
    [TestFixture]
    public class EditorApiTests
    {
        [Test]
        public void EditorApi_IsAccessibleFromLua()
        {
            // This test is obsolete as EditorApi and the old Scripting class have been removed.
            // Kept as a placeholder.
            Assert.Pass();
        }
    }
}
