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

        [Test]
        public void MathProcs_WithInvalidInput_DoesNotCrash()
        {
            var nativeProvider = new StandardNativeProcProvider();
            var nativeProcs = nativeProvider.GetNativeProcs();
            var absProc = (NativeProc)nativeProcs["abs"];
            var sinProc = (NativeProc)nativeProcs["sin"];

            // Should return Null or 0, but NOT throw
            Assert.DoesNotThrow(() =>
            {
                var result = absProc.Call(null!, null, new[] { new DreamValue("not a number") });
                Assert.That(result.Type, Is.EqualTo(DreamValueType.Float));
                Assert.That(result.RawFloat, Is.EqualTo(0f));
            });

            Assert.DoesNotThrow(() =>
            {
                var result = sinProc.Call(null!, null, new[] { DreamValue.Null });
                Assert.That(result.Type, Is.EqualTo(DreamValueType.Float));
                Assert.That(result.RawFloat, Is.EqualTo(0f));
            });
        }

        [Test]
        public void ArcTan_NativeProc_SupportsOverloads()
        {
            var nativeProvider = new StandardNativeProcProvider();
            var nativeProcs = nativeProvider.GetNativeProcs();
            var arctanProc = (NativeProc)nativeProcs["arctan"];

            // arctan(1) -> 45 degrees
            var res1 = arctanProc.Call(null!, null, new[] { new DreamValue(1f) });
            Assert.That(res1.AsFloat(), Is.EqualTo(45f).Within(0.001f));

            // arctan(1, 1) -> 45 degrees
            var res2 = arctanProc.Call(null!, null, new[] { new DreamValue(1f), new DreamValue(1f) });
            Assert.That(res2.AsFloat(), Is.EqualTo(45f).Within(0.001f));

            // arctan(0, 1) -> 90 degrees
            var res3 = arctanProc.Call(null!, null, new[] { new DreamValue(0f), new DreamValue(1f) });
            Assert.That(res3.AsFloat(), Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void DreamList_RemoveValue_UpdatesAssociativeValues()
        {
            var list = new DreamList(null);
            var key = new DreamValue("key");
            var val = new DreamValue("value");

            list.SetValue(key, val);
            Assert.That(list.GetValue(key), Is.EqualTo(val));
            Assert.That(list.Contains(key), Is.True);

            list.RemoveValue(key);
            Assert.That(list.GetValue(key), Is.EqualTo(DreamValue.Null));
            Assert.That(list.Contains(key), Is.False);
        }

        [Test]
        public void DreamList_SetValueIndex_UpdatesAssociativeValues()
        {
            var list = new DreamList(null);
            var key = new DreamValue("key");
            var val = new DreamValue("value");

            list.AddValue(key);
            list.SetValue(key, val);
            Assert.That(list.GetValue(key), Is.EqualTo(val));

            list.SetValue(0, new DreamValue("other"));
            Assert.That(list.GetValue(key), Is.EqualTo(DreamValue.Null));
            Assert.That(list.Contains(key), Is.False);
        }

        [Test]
        public void DreamObject_ToString_ReturnsTypeName()
        {
            var type = new ObjectType(1, "/obj/item");
            var obj = new DreamObject(type);
            var val = new DreamValue(obj);

            Assert.That(val.ToString(), Is.EqualTo("/obj/item"));
        }
    }
}
