using NUnit.Framework;
using Core;
using System.Collections.Generic;

namespace tests
{
[TestFixture]
public class DmParserTests
{
private ObjectTypeManager _typeManager = null!;
private DmParser _parser = null!;

[SetUp]
    public void SetUp()
    {
        _typeManager = new ObjectTypeManager();
        _parser = new DmParser(_typeManager);
    }

    [Test]
    public void ParseString_RegistersSimpleType()
    {
        // Arrange
        string dmCode = @"/obj/item";
// Act
_parser.ParseString(dmCode);

// Assert
        var type = _typeManager.GetObjectType("/obj/item");
        Assert.That(type, Is.Not.Null);
        Assert.That(type!.Name, Is.EqualTo("/obj/item"));
    }

    [Test]
    public void ParseString_RegistersNestedTypesIndented()
    {
        // Arrange
        string dmCode = @"/obj
	item
		weapon";
// Act
_parser.ParseString(dmCode);

// Assert
        var obj = _typeManager.GetObjectType("/obj");
        var item = _typeManager.GetObjectType("/obj/item");
        var weapon = _typeManager.GetObjectType("/obj/item/weapon");

        Assert.That(obj, Is.Not.Null);
        Assert.That(item, Is.Not.Null);
        Assert.That(weapon, Is.Not.Null);

        Assert.That(item!.ParentName, Is.EqualTo("/obj"));
        Assert.That(item.Parent, Is.EqualTo(obj));

        Assert.That(weapon!.ParentName, Is.EqualTo("/obj/item"));
        Assert.That(weapon.Parent, Is.EqualTo(item));
    }

    [Test]
    public void ParseString_ParsesProperties()
    {
        // Arrange
        string dmCode = @"/obj/item
	name = ""Generic Item""
	weight = 10
	icon = 'icons/obj.dmi'";
// Act
_parser.ParseString(dmCode);

// Assert
        var type = _typeManager.GetObjectType("/obj/item");
        Assert.That(type, Is.Not.Null);

        Assert.That(type!.DefaultProperties.ContainsKey("name"), Is.True);
        Assert.That(type.DefaultProperties["name"], Is.EqualTo("Generic Item"));

        Assert.That(type.DefaultProperties["weight"], Is.EqualTo(10));
        Assert.That(type.DefaultProperties["icon"], Is.EqualTo("icons/obj.dmi"));
    }

    [Test]
    public void ParseString_NestedPropertiesAndTypes()
    {
        // Arrange
        string dmCode = @"/obj/item
	name = ""Item""
	weapon
		name = ""Weapon""
		damage = 50";
// Act
_parser.ParseString(dmCode);

// Assert
        var item = _typeManager.GetObjectType("/obj/item");
        var weapon = _typeManager.GetObjectType("/obj/item/weapon");

        Assert.That(item!.DefaultProperties["name"], Is.EqualTo("Item"));
        Assert.That(weapon!.DefaultProperties["name"], Is.EqualTo("Weapon"));
        Assert.That(weapon.DefaultProperties["damage"], Is.EqualTo(50));
    }

    [Test]
    public void ParseString_MixedIndentations()
    {
        // Check if 4 spaces behave like tabs (based on parser logic)
        string dmCode = "/obj\n    child";
        _parser.ParseString(dmCode);

        Assert.That(_typeManager.GetObjectType("/obj/child"), Is.Not.Null);
    }

    [Test]
    public void ParseString_CapturesProcedureBody()
    {
        // Arrange
        string dmCode = @"
/mob/proc/Attack()
    set src in oview(1)
    usr << ""You attack [src]!""
    sleep(10)
";

        string expectedBody = @"    set src in oview(1)
    usr << ""You attack [src]!""
    sleep(10)";

        // Act
        _parser.ParseString(dmCode);

        // Assert
        var mobType = _typeManager.GetObjectType("/mob");
        Assert.That(mobType, Is.Not.Null);
        Assert.That(mobType.DmProcedures.ContainsKey("Attack"), Is.True);
        Assert.That(mobType.DmProcedures["Attack"], Is.EqualTo(expectedBody));
    }

    [Test]
    public void ParseString_HandlesInlineProcDefinition()
    {
        // Arrange
        string dmCode = @"/obj/item/weapon/sword/proc/OnAttack()
	usr << ""Swish!""";

        // Act
        _parser.ParseString(dmCode);

        // Assert
        var swordType = _typeManager.GetObjectType("/obj/item/weapon/sword");
        Assert.That(swordType, Is.Not.Null, "Sword type should be registered");
        Assert.That(swordType.DmProcedures.ContainsKey("OnAttack"), Is.True, "OnAttack proc should be captured");
        Assert.That(swordType.DmProcedures["OnAttack"], Is.EqualTo("\tusr << \"Swish!\""));
    }
}

}
