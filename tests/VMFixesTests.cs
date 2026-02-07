using Shared;
using Shared.Models;
using NUnit.Framework;
using Core.VM;
using Core;
using System.Collections.Generic;
using Core.VM.Runtime;
using Core.VM.Procs;
using System;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Core.Objects;

namespace tests
{
    [TestFixture]
    public class VMFixesTests
    {
        private DreamVM _vm = null!;

        [SetUp]
        public void SetUp()
        {
            _vm = new DreamVM(Options.Create(new ServerSettings()), NullLogger<DreamVM>.Instance, new INativeProcProvider[] { new StandardNativeProcProvider() });
        }

        [Test]
        public void IsTypeDirect_NullManager_DoesNotCrash()
        {
            _vm.Context.ObjectTypeManager = null;
            var bytecode = new List<byte> { (byte)Opcode.PushFloat };
            bytecode.AddRange(BitConverter.GetBytes(0f)); // Push 0 (not an object)
            bytecode.Add((byte)Opcode.IsTypeDirect);
            bytecode.AddRange(BitConverter.GetBytes(1)); // typeId
            bytecode.Add((byte)Opcode.Return);

            var proc = new DreamProc("test", bytecode.ToArray(), Array.Empty<string>(), 0);
            var thread = new DreamThread(proc, _vm.Context, 1000);

            Assert.DoesNotThrow(() => thread.Run(1000));
            Assert.That(thread.Pop(), Is.EqualTo(DreamValue.False));
        }

        [Test]
        public void CreateObject_PersistenceTest()
        {
            var manager = new ObjectTypeManager(NullLogger<ObjectTypeManager>.Instance);
            _vm.Context.ObjectTypeManager = manager;
            _vm.Context.ListType = new ObjectType(0, "/list");

            var type = new ObjectType(1, "/test");
            manager.RegisterObjectType(type);

            // Define a New proc that returns something else (to test if it overwrites the object)
            var newBytecode = new List<byte> { (byte)Opcode.PushFloat };
            newBytecode.AddRange(BitConverter.GetBytes(123f));
            newBytecode.Add((byte)Opcode.Return);
            var newProc = new DreamProc("New", newBytecode.ToArray(), Array.Empty<string>(), 0);
            type.Procs["New"] = newProc;

            // Main test bytecode: new /test()
            var bytecode = new List<byte> { (byte)Opcode.PushType };
            bytecode.AddRange(BitConverter.GetBytes(1)); // typeId 1 (/test)
            bytecode.Add((byte)Opcode.CreateObject);
            bytecode.Add((byte)(byte)DMCallArgumentsType.None);
            bytecode.AddRange(BitConverter.GetBytes(1)); // 0 args + 1 type = stack delta 1
            bytecode.Add((byte)Opcode.Return);

            var proc = new DreamProc("test", bytecode.ToArray(), Array.Empty<string>(), 0);
            var thread = new DreamThread(proc, _vm.Context, 1000);

            thread.Run(1000);
            var result = thread.Pop();

            Assert.That(result.Type, Is.EqualTo(DreamValueType.DreamObject));
            Assert.That(result.GetValueAsDreamObject()?.ObjectType?.Name, Is.EqualTo("/test"));
            // If the bug was present, result would be 123
        }

        [Test]
        public void CallStatement_DelegationTest()
        {
            var manager = new ObjectTypeManager(NullLogger<ObjectTypeManager>.Instance);
            _vm.Context.ObjectTypeManager = manager;

            var parentType = new ObjectType(1, "/parent");
            var childType = new ObjectType(2, "/child") { Parent = parentType };
            manager.RegisterObjectType(parentType);
            manager.RegisterObjectType(childType);

            // Parent proc: returns 42
            var parentBytecode = new List<byte> { (byte)Opcode.PushFloat };
            parentBytecode.AddRange(BitConverter.GetBytes(42f));
            parentBytecode.Add((byte)Opcode.Return);
            var attackProcParent = new DreamProc("attack", parentBytecode.ToArray(), Array.Empty<string>(), 0);
            parentType.Procs["attack"] = attackProcParent;

            // Child proc: calls ..() and returns its result
            var childBytecode = new List<byte> { (byte)Opcode.CallStatement };
            childBytecode.Add((byte)(byte)DMCallArgumentsType.None);
            childBytecode.AddRange(BitConverter.GetBytes(0)); // 0 args
            childBytecode.Add((byte)Opcode.Return);
            var attackProcChild = new DreamProc("attack", childBytecode.ToArray(), Array.Empty<string>(), 0);
            childType.Procs["attack"] = attackProcChild;

            // Execute attackProcChild on a /child instance
            var obj = new GameObject(childType);
            var thread = new DreamThread(attackProcChild, _vm.Context, 1000, obj);

            thread.Run(1000);
            var result = thread.Pop();

            Assert.That(result.AsFloat(), Is.EqualTo(42f));
        }
    }
}
