using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shared;
using Shared.Services;
using Shared.Interfaces;
using Shared.Utils;
using Shared.Enums;
using Shared.Networking;
using Shared.Networking.Messages;
using Shared.Networking.Handlers;
using Shared.Models;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Tests;

[TestFixture]
public class NetworkingTests
{
    private BinarySnapshotService _snapshotService;
    private BitPackedSnapshotSerializer _serializer;
    private Mock<IObjectTypeManager> _typeManagerMock;
    private Mock<IObjectFactory> _factoryMock;
    private ServiceCollection _services;
    private ServiceProvider _serviceProvider;

    [SetUp]
    public void SetUp()
    {
        var fieldHandlers = new List<INetworkFieldHandler>
        {
            new Shared.Networking.FieldHandlers.TypeFieldHandler(),
            new Shared.Networking.FieldHandlers.TransformFieldHandler(),
            new Shared.Networking.FieldHandlers.VisualFieldHandler(),
            new Shared.Networking.FieldHandlers.VariablesFieldHandler(),
            new Shared.Networking.FieldHandlers.ComponentsFieldHandler()
        };
        _serializer = new BitPackedSnapshotSerializer(fieldHandlers);
        _snapshotService = new BinarySnapshotService(_serializer);
        _typeManagerMock = new Mock<IObjectTypeManager>();
        _factoryMock = new Mock<IObjectFactory>();

        _services = new ServiceCollection();
        _services.AddSingleton<ISnapshotSerializer>(_serializer);
        _services.AddSingleton<IObjectTypeManager>(_typeManagerMock.Object);
        _services.AddSingleton<IObjectFactory>(_factoryMock.Object);
        _services.AddSingleton<ISnapshotManager>(new Mock<ISnapshotManager>().Object);
        _services.AddSingleton<ILogger<ClientInputMessageHandler>>(new Mock<ILogger<ClientInputMessageHandler>>().Object);
        _services.AddSingleton<IMessageHandler, ClientInputMessageHandler>();

        _serviceProvider = _services.BuildServiceProvider();
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
        var writer = new BitWriter(buffer);
        _snapshotService.SerializeBitPackedDelta(ref writer, objects, null);
        int written = writer.BytesWritten;
        Assert.That(written, Is.GreaterThan(0));

        byte[] data = new byte[written];
        Array.Copy(buffer, data, written);

        // Prepare world
        var world = new Dictionary<long, GameObject>();
        var targetObj = new GameObject(type) { Id = 101, Version = 0 };
        world[101] = targetObj;

        // Deserialize
        var reader = new BitReader(data);
        _snapshotService.DeserializeBitPacked(ref reader, world, _typeManagerMock.Object, _factoryMock.Object);

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

        var writer = new BitWriter(buffer);
        _snapshotService.SerializeBitPackedDelta(ref writer, objects, null);
        byte[] data = new byte[writer.BytesWritten];
        Array.Copy(buffer, data, writer.BytesWritten);

        var world = new Dictionary<long, GameObject>();
        var targetObj = new GameObject(type) { Id = 303 };
        targetObj.SetPosition(10, 10, 10); // Start elsewhere
        world[303] = targetObj;

        // Deserialize
        var reader = new BitReader(data);
        _snapshotService.DeserializeBitPacked(ref reader, world, _typeManagerMock.Object, _factoryMock.Object);

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

        var writer = new BitWriter(buffer);
        _snapshotService.SerializeBitPackedDelta(ref writer, objects, lastVersions);
        byte[] data = new byte[writer.BytesWritten];
        Array.Copy(buffer, data, writer.BytesWritten);

        var world = new Dictionary<long, GameObject>();

        // Deserialize
        var reader = new BitReader(data);
        _snapshotService.DeserializeBitPacked(ref reader, world, _typeManagerMock.Object, _factoryMock.Object);

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

        var writer = new BitWriter(buffer);
        _snapshotService.SerializeBitPackedDelta(ref writer, objects, lastVersions);

        // Analyze buffer
        var reader = new BitReader(buffer);
        reader.ReadVarInt(); // Id
        reader.ReadVarInt(); // Version
        GameObjectFields mask = (GameObjectFields)reader.ReadBits(32);

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

    [Test]
    public void NetworkMessageHandler_DispatchesCorrectly()
    {
        var handler = new TestMessageHandler();

        var services = new ServiceCollection();
        services.AddSingleton<IMessageHandler>(handler);
        var serviceProvider = services.BuildServiceProvider();

        var dispatcher = new NetworkMessageHandler(serviceProvider.GetServices<IMessageHandler>());

        var inputMsg = new ClientInputMessage { InputType = ClientInputType.Move, X = 1, Y = 2 };
        byte[] buffer = new byte[1024];
        var writer = new BitWriter(buffer);
        writer.WriteByte(inputMsg.MessageTypeId);
        inputMsg.Write(ref writer);

        var data = new ReadOnlyMemory<byte>(buffer, 0, writer.BytesWritten);
        var peerMock = new Mock<INetworkPeer>();

        dispatcher.HandleAsync(peerMock.Object, data).Wait();

        Assert.That(handler.WasCalled, Is.True);
    }

    private class TestMessageHandler : IMessageHandler
    {
        public byte MessageTypeId => (byte)ClientMessageType.Input;
        public bool WasCalled { get; private set; }

        public ValueTask HandleAsync(INetworkPeer peer, ReadOnlyMemory<byte> data)
        {
            WasCalled = true;
            return ValueTask.CompletedTask;
        }
    }

    private class SyncableComponent : BaseComponent
    {
        private int _value;
        public int Value { get => _value; set { _value = value; IsDirty = true; } }
        public override void WriteState(ref BitWriter writer) { Console.WriteLine($"Writing Value: {Value}"); writer.WriteVarInt(Value); }
        public override void ReadState(ref BitReader reader) { _value = (int)reader.ReadVarInt(); Console.WriteLine($"Read Value: {_value}"); }
        public override void Reset() { base.Reset(); _value = 0; }
    }

    [Test]
    public void BitPackedProtocol_SyncsComponentState()
    {
        var type = CreateTestType();
        var diagnosticBus = new MockDiagnosticBus();
        var archetypeManager = new ArchetypeManager(Microsoft.Extensions.Logging.Abstractions.NullLogger<ArchetypeManager>.Instance, diagnosticBus);
        var componentManager = new ComponentManager(archetypeManager);

        var obj = new GameObject(type) { Id = 606 };
        obj.SetComponentManager(componentManager);
        var comp = new SyncableComponent { Value = 42 };
        obj.AddComponent(comp);
        comp.IsDirty = true; // Ensure it's marked as dirty

        var objects = new List<IGameObject> { obj };
        byte[] buffer = new byte[1024];
        var writer = new BitWriter(buffer);
        _snapshotService.SerializeBitPackedDelta(ref writer, objects, null);

        var data = new byte[writer.BytesWritten];
        Array.Copy(buffer, data, writer.BytesWritten);

        var world = new Dictionary<long, GameObject>();
        var targetObj = new GameObject(type) { Id = 606 };
        targetObj.SetComponentManager(componentManager);
        var targetComp = new SyncableComponent();
        targetObj.AddComponent(targetComp);
        world[606] = targetObj;

        var reader = new BitReader(data);
        Console.WriteLine($"Starting deserialization of {data.Length} bytes");

        // Print mask for debugging
        var debugReader = new BitReader(data);
        debugReader.ReadVarInt(); // ID
        debugReader.ReadVarInt(); // Version
        var mask = (GameObjectFields)debugReader.ReadBits(32); // Updated to 32-bit
        Console.WriteLine($"Mask: {mask}");

        _snapshotService.DeserializeBitPacked(ref reader, world, _typeManagerMock.Object, _factoryMock.Object);

        Assert.That(targetComp.Value, Is.EqualTo(42));
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
    }
}
