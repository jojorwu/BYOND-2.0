using Shared;
using NUnit.Framework;
using Core;
using System.IO;
using System.Text.Json;
using Core.Projects;

namespace Core.Tests
{
    [TestFixture]
    public class ProjectTests
    {
        private string _testProjectPath = null!;

        [SetUp]
        public void SetUp()
        {
            _testProjectPath = Path.Combine(Path.GetTempPath(), "TestProject");
            if (Directory.Exists(_testProjectPath))
            {
                Directory.Delete(_testProjectPath, true);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testProjectPath))
            {
                Directory.Delete(_testProjectPath, true);
            }
        }

        [Test]
        public void Create_ShouldCreateProjectDirectoriesAndFiles()
        {
            // Act
            var project = Project.Create(_testProjectPath);

            // Assert
            Assert.That(Directory.Exists(_testProjectPath), Is.True);
            Assert.That(Directory.Exists(project.GetFullPath("maps")), Is.True);
            Assert.That(Directory.Exists(project.GetFullPath("scripts")), Is.True);
            Assert.That(Directory.Exists(project.GetFullPath("assets")), Is.True);
            Assert.That(File.Exists(project.GetFullPath("project.json")), Is.True);
            Assert.That(File.Exists(project.GetFullPath("server_config.json")), Is.True);
        }

        [Test]
        public void Create_ShouldCreateDefaultServerConfigFile()
        {
            // Act
            var project = Project.Create(_testProjectPath);

            // Assert
            var configPath = project.GetFullPath("server_config.json");
            var json = File.ReadAllText(configPath);
            var settings = JsonSerializer.Deserialize<ServerSettings>(json);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.Network.IpAddress, Is.EqualTo("127.0.0.1"));
            Assert.That(settings.Network.UdpPort, Is.EqualTo(9050));
            Assert.That(settings.Threading.Mode, Is.EqualTo(ThreadMode.Automatic));
        }
    }
}
