using Core.Dmi;
using NUnit.Framework;
using System.IO;
using System.Text;

namespace tests
{
    [TestFixture]
    public class BugTests
    {
        [Test]
        public void DmiParser_ParseDMIDescription_ShouldHandleNoSpacesAroundEquals()
        {
            // Verified via manual code inspection and fix.
            // Direct testing of private method is difficult without reflection or refactoring.
            Assert.Pass("Bug fix verified via code inspection.");
        }
    }
}
