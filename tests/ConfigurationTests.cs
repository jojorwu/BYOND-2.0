using NUnit.Framework;
using Shared.Config;
using System.Collections.Generic;

namespace tests
{
    [TestFixture]
    public class ConfigurationTests
    {
        private ConfigurationManager _manager;

        [SetUp]
        public void SetUp()
        {
            _manager = new ConfigurationManager();
        }

        [Test]
        public void TestRegisterAndGet()
        {
            _manager.RegisterCVar("Test.Bool", true);
            Assert.That(_manager.GetCVar<bool>("Test.Bool"), Is.True);
        }

        [Test]
        public void TestSetAndEvent()
        {
            _manager.RegisterCVar("Test.Int", 10);
            string? changedKey = null;
            object? changedValue = null;
            _manager.OnCVarChanged += (key, val) => {
                changedKey = key;
                changedValue = val;
            };

            _manager.SetCVar("Test.Int", 20);
            Assert.That(_manager.GetCVar<int>("Test.Int"), Is.EqualTo(20));
            Assert.That(changedKey, Is.EqualTo("Test.Int"));
            Assert.That(changedValue, Is.EqualTo(20));
        }

        [Test]
        public void TestJsonPersistence()
        {
            var path = "test_config.json";
            _manager.RegisterCVar("Test.String", "Hello", CVarFlags.Archive);
            _manager.SetCVar("Test.String", "World");
            _manager.Save(path);

            var newManager = new ConfigurationManager();
            newManager.RegisterCVar("Test.String", "Default");
            newManager.Load(path);

            Assert.That(newManager.GetCVar<string>("Test.String"), Is.EqualTo("World"));

            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }

        [Test]
        public void TestTypeSafety()
        {
            _manager.RegisterCVar("Test.Int", 10);
            Assert.Throws<System.InvalidCastException>(() => _manager.GetCVar<string>("Test.Int"));
        }

        [Test]
        public void TestSetCVarDirect_ConvertsTypes()
        {
            _manager.RegisterCVar("Test.Int", 10);
            _manager.RegisterCVar("Test.Bool", false);
            _manager.RegisterCVar("Test.Float", 1.0f);

            _manager.SetCVarDirect("Test.Int", "20");
            _manager.SetCVarDirect("Test.Bool", "true");
            _manager.SetCVarDirect("Test.Float", "3.14");

            Assert.That(_manager.GetCVar<int>("Test.Int"), Is.EqualTo(20));
            Assert.That(_manager.GetCVar<bool>("Test.Bool"), Is.True);
            Assert.That(_manager.GetCVar<float>("Test.Float"), Is.EqualTo(3.14f).Within(0.001f));
        }
    }
}
