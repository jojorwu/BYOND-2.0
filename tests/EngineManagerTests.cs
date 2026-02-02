using Shared.Models;
using Shared.Enums;
using Shared.Operations;
using NUnit.Framework;
using Shared.Interfaces;
using Shared.Services;
using System.IO;

namespace tests
{
    [TestFixture]
    public class EngineManagerTests
    {
        private string _testDir;
        private string _settingsFile;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_testDir);
            _settingsFile = "launcher_settings.json";
            if (File.Exists(_settingsFile)) File.Delete(_settingsFile);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true);
            if (File.Exists(_settingsFile)) File.Delete(_settingsFile);
        }

        [Test]
        public void SetBaseEnginePath_SavesToSettings()
        {
            var manager = new EngineManager();
            manager.SetBaseEnginePath(_testDir);

            Assert.That(File.Exists(_settingsFile), Is.True);

            var newManager = new EngineManager();
            Assert.That(newManager.GetBaseEnginePath(), Is.EqualTo(_testDir));
        }

        [Test]
        public void GetExecutablePath_ReturnsCorrectPath()
        {
            var manager = new EngineManager();
            manager.SetBaseEnginePath(_testDir);

            string expectedName = "Client";
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                expectedName += ".exe";
            }

            var path = manager.GetExecutablePath(EngineComponent.Client);
            Assert.That(path, Does.Contain(expectedName));
        }
    }
}
