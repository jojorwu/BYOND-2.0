using NUnit.Framework;
using Core;

namespace Core.Tests
{
    [TestFixture]
    public class LuaPropertyBindingTests
    {
        private Scripting _scripting = null!;
        private GameApi _gameApi = null!;
        private GameState _gameState = null!;
        private ObjectTypeManager _objectTypeManager = null!;

        [SetUp]
        public void SetUp()
        {
            _gameState = new GameState();
            _objectTypeManager = new ObjectTypeManager();
            _gameApi = new GameApi(_gameState, _objectTypeManager);
            _scripting = new Scripting(_gameApi);

            var testObjectType = new ObjectType("test");
            testObjectType.DefaultProperties["health"] = 100;
            _objectTypeManager.RegisterObjectType(testObjectType);
        }

        [TearDown]
        public void TearDown()
        {
            _scripting.Dispose();
        }

        [Test]
        public void Lua_CanSetAndGetObjectProperties()
        {
            // Arrange
            var obj = _gameApi.CreateObject("test", 0, 0, 0);
            Assert.That(obj, Is.Not.Null);

            var script = $@"
                Game:SetObjectProperty({obj.Id}, 'test_prop', 123)
                local val = Game:GetObjectProperty({obj.Id}, 'test_prop')
                assert(val == 123, 'Property value should be 123')
            ";

            // Act & Assert
            Assert.DoesNotThrow(() => _scripting.ExecuteString(script));
        }

        [Test]
        public void Lua_CanGetDefaultProperties()
        {
            // Arrange
            var obj = _gameApi.CreateObject("test", 0, 0, 0);
            Assert.That(obj, Is.Not.Null);

            var script = $@"
                local val = Game:GetObjectProperty({obj.Id}, 'health')
                assert(val == 100, 'Default property value should be 100')
            ";

            // Act & Assert
            Assert.DoesNotThrow(() => _scripting.ExecuteString(script));
        }
    }
}
