using Shared;
using NUnit.Framework;
using Core;

namespace tests
{
    [TestFixture]
    public class GameObjectSyncTests
    {
        [Test]
        public void SetVariable_Coordinates_ShouldUpdateProperties()
        {
            var type = new ObjectType(1, "/obj");
            type.VariableNames.Add("x");
            type.VariableNames.Add("y");
            type.VariableNames.Add("z");
            type.FlattenedDefaultValues.Add(0f);
            type.FlattenedDefaultValues.Add(0f);
            type.FlattenedDefaultValues.Add(0f);

            var obj = new GameObject(type);
            obj.SetVariable("x", 10f);
            obj.SetVariable("y", 20f);
            obj.SetVariable("z", 30f);

            Assert.That(obj.X, Is.EqualTo(10), "X property should be updated");
            Assert.That(obj.Y, Is.EqualTo(20), "Y property should be updated");
            Assert.That(obj.Z, Is.EqualTo(30), "Z property should be updated");
        }

        [Test]
        public void PropertyChange_ShouldUpdateVariableArray()
        {
            var type = new ObjectType(1, "/obj");
            type.VariableNames.Add("x");
            type.FlattenedDefaultValues.Add(0f);

            var obj = new GameObject(type);
            obj.X = 50;

            Assert.That(obj.GetVariable("x").AsFloat(), Is.EqualTo(50f), "Variable 'x' should match property X");
        }

        [Test]
        public void LocChange_ShouldUpdateContents()
        {
            var type = new ObjectType(1, "/obj");
            var obj1 = new GameObject(type);
            var obj2 = new GameObject(type);

            obj1.Loc = obj2;

            Assert.That(obj2.Contents, Contains.Item(obj1));
            Assert.That(obj1.Loc, Is.EqualTo(obj2));

            obj1.Loc = null;
            Assert.That(obj2.Contents, Is.Empty);
        }

        [Test]
        public void AddContent_ShouldUpdateLoc()
        {
            var type = new ObjectType(1, "/obj");
            var obj1 = new GameObject(type);
            var obj2 = new GameObject(type);

            obj2.AddContent(obj1);

            Assert.That(obj1.Loc, Is.EqualTo(obj2));
            Assert.That(obj2.Contents, Contains.Item(obj1));
        }
    }
}
