using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shared;
using Shared.Services;
using Shared.Interfaces;
using Shared.Utils;
using Shared.Enums;
using Shared.Networking.Messages;
using Moq;

namespace Tests;

[TestFixture]
public class NetworkingTests
{
    private BinarySnapshotService _snapshotService;
    private BitPackedSnapshotSerializer _serializer;
    private Mock<IObjectTypeManager> _typeManagerMock;
    private Mock<IObjectFactory> _factoryMock;

    [SetUp]
    public void SetUp()
    {
        _serializer = new BitPackedSnapshotSerializer();
        _snapshotService = new BinarySnapshotService(_serializer);
        _typeManagerMock = new Mock<IObjectTypeManager>();
        _factoryMock = new Mock<IObjectFactory>();
    }

    private ObjectType CreateTestType()
    {
        var type = new ObjectType(1, "test_type");
        type.VariableNames.Add("icon");
        type.FlattenedDefaultValues.Add(DreamValue.Null);
        type.VariableNames.Add("icon_state");
        type.FlattenedDefaultValues.Add(DreamValue.Null);
        type.VariableNames.Add("dir");
        type.FlattenedDefaultValues.Add(new DreamValue(2.0));
        type.VariableNames.Add("alpha");
        type.FlattenedDefaultValues.Add(new DreamValue(255.0));
        type.VariableNames.Add("color");
        type.FlattenedDefaultValues.Add(new DreamValue("#ffffff"));
        type.VariableNames.Add("layer");
        type.FlattenedDefaultValues.Add(new DreamValue(2.0));

        type.FinalizeVariables();
        return type;
    }

    [Test]
    public void BitPackedProtocol_Serialization_Deserialization_IsAccurate()
    {
        // Setup
        var type = CreateTestType();
        var obj = new GameObject(type) { Id = 101 };
        obj.Version = 5;
        obj.SetPosition(10, 20, 30);
        obj.Dir = 4;
        obj.Alpha = 128.5;
        obj.Icon = "test_icon";

        long expectedVersion = obj.Version;

        var objects = new List<IGameObject> { obj };
        byte[] buffer = new byte[1024];

        // Serialize
        int written = _snapshotService.SerializeBitPackedDelta(buffer, objects, null, out bool truncated);
        Assert.That(truncated, Is.False);
        Assert.That(written, Is.GreaterThan(0));

        byte[] data = new byte[written];
        Array.Copy(buffer, data, written);

        // Prepare world
        var world = new Dictionary<long, GameObject>();
        var targetObj = new GameObject(type) { Id = 101, Version = 0 };
        world[101] = targetObj;

        // Deserialize
        _snapshotService.DeserializeBitPacked(data, world, _typeManagerMock.Object, _factoryMock.Object);

        // Verify
        Assert.That(targetObj.Id, Is.EqualTo(101L));
        Assert.That(targetObj.Version, Is.EqualTo(expectedVersion));
        Assert.That(targetObj.X, Is.EqualTo(10L));
        Assert.That(targetObj.Y, Is.EqualTo(20L));
        Assert.That(targetObj.Z, Is.EqualTo(30L));
        Assert.That(targetObj.Dir, Is.EqualTo(4));
        Assert.That(targetObj.Alpha, Is.EqualTo(128.5));
        Assert.That(targetObj.Icon, Is.EqualTo("test_icon"));
    }

    [Test]
    public void BitPackedProtocol_HandlesOriginPosition()
    {
        // Setup
        var type = CreateTestType();
        var obj = new GameObject(type) { Id = 303 };
        obj.SetPosition(0, 0, 0); // Origin

        var objects = new List<IGameObject> { obj };
        byte[] buffer = new byte[1024];

        int written = _snapshotService.SerializeBitPackedDelta(buffer, objects, null, out _);
        byte[] data = new byte[written];
        Array.Copy(buffer, data, written);

        var world = new Dictionary<long, GameObject>();
        var targetObj = new GameObject(type) { Id = 303 };
        targetObj.SetPosition(10, 10, 10); // Start elsewhere
        world[303] = targetObj;

        // Deserialize
        _snapshotService.DeserializeBitPacked(data, world, _typeManagerMock.Object, _factoryMock.Object);

        // Verify
        Assert.That(targetObj.X, Is.EqualTo(0L));
        Assert.That(targetObj.Y, Is.EqualTo(0L));
        Assert.That(targetObj.Z, Is.EqualTo(0L));
    }

    [Test]
    public void BitPackedProtocol_HandlesNewObjectSpawning()
    {
        // Setup
        var type = CreateTestType();
        _typeManagerMock.Setup(m => m.GetObjectType(type.Id)).Returns(type);

        var obj = new GameObject(type) { Id = 404 };
        obj.SetPosition(5, 5, 5);
        _factoryMock.Setup(f => f.Create(type, 0, 0, 0)).Returns(new GameObject(type));

        var objects = new List<IGameObject> { obj };
        byte[] buffer = new byte[1024];
        var lastVersions = new Dictionary<long, long>();

        int written = _snapshotService.SerializeBitPackedDelta(buffer, objects, lastVersions, out _);
        byte[] data = new byte[written];
        Array.Copy(buffer, data, written);

        var world = new Dictionary<long, GameObject>();

        // Deserialize
        _snapshotService.DeserializeBitPacked(data, world, _typeManagerMock.Object, _factoryMock.Object);

        // Verify
        Assert.That(world.ContainsKey(404), Is.True);
        Assert.That(world[404].X, Is.EqualTo(5L));
    }

    [Test]
    public void BitPackedProtocol_OnlySerializesChangedFields()
    {
        // Setup
        var type = CreateTestType();
        var obj = new GameObject(type) { Id = 505, Version = 10 };
        obj.ClearChangeMask(); // Mock clean state

        // Only change X
        obj.X = 42;

        var lastVersions = new Dictionary<long, long> { { 505, 9 } };
        var objects = new List<IGameObject> { obj };
        byte[] buffer = new byte[1024];

        int written = _snapshotService.SerializeBitPackedDelta(buffer, objects, lastVersions, out _);

        // Analyze buffer
        var reader = new BitReader(buffer);
        reader.ReadVarInt(); // Id
        reader.ReadVarInt(); // Version
        GameObjectFields mask = (GameObjectFields)reader.ReadBits(16);

        Assert.That((mask & GameObjectFields.PositionX), Is.Not.EqualTo(GameObjectFields.None));
        Assert.That((mask & GameObjectFields.PositionY), Is.EqualTo(GameObjectFields.None));
        Assert.That((mask & GameObjectFields.Visuals), Is.EqualTo(GameObjectFields.None));
    }

    [Test]
    public void MessageBasedArchitecture_SoundMessage_Works()
    {
        var sound = new SoundData("test.ogg", 50f, 1.2f, true);
        sound.X = 10;
        var msg = new SoundMessage { Data = sound };

        byte[] buffer = new byte[1024];
        var writer = new BitWriter(buffer);
        msg.Write(ref writer);

        var reader = new BitReader(buffer);
        var msg2 = new SoundMessage();
        msg2.Read(ref reader);

        Assert.That(msg2.Data.File, Is.EqualTo("test.ogg"));
        Assert.That(msg2.Data.Volume, Is.EqualTo(50f));
        Assert.That(msg2.Data.X, Is.EqualTo(10L));
        Assert.That(msg2.Data.Repeat, Is.True);
    }
}
