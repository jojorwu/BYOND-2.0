using Shared.Enums;
using Shared;

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
using Shared.Services;

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

        [TearDown]
        public void TearDown()
        {
            _vm.Dispose();
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

        [Test]
        public void Modulo_WorksWithNegativeNumbers()
        {
            // Remainder (%) follows sign of dividend
            Assert.That((new DreamValue(-11f) % new DreamValue(3f)).AsFloat(), Is.EqualTo(-2f));
            Assert.That((new DreamValue(11f) % new DreamValue(-3f)).AsFloat(), Is.EqualTo(2f));

            // Modulo (%%) follows sign of divisor
            Assert.That(SharedOperations.Modulo(-11f, 3f), Is.EqualTo(1f));
            Assert.That(SharedOperations.Modulo(11f, -3f), Is.EqualTo(-1f));
        }

        [Test]
        public void Equality_StrictEquals_FuzzyOperator()
        {
            var nullVal = DreamValue.Null;
            var zeroVal = new DreamValue(0f);

            // Strict Equals (used by Dictionary)
            Assert.That(nullVal.Equals(zeroVal), Is.False);

            // Fuzzy operator (used by VM scripts)
            Assert.That(nullVal == zeroVal, Is.True);

            var dict = new Dictionary<DreamValue, int>();
            dict[nullVal] = 1;
            dict[zeroVal] = 2;

            Assert.That(dict.Count, Is.EqualTo(2));
            Assert.That(dict[nullVal], Is.EqualTo(1));
            Assert.That(dict[zeroVal], Is.EqualTo(2));
        }

        [Test]
        public void Call_FromVariable_Works()
        {
            var parentBytecode = new List<byte> { (byte)Opcode.PushFloat };
            parentBytecode.AddRange(BitConverter.GetBytes(42f));
            parentBytecode.Add((byte)Opcode.Return);
            var proc = new DreamProc("test_proc", parentBytecode.ToArray(), Array.Empty<string>(), 0);

            // test_proc() call where test_proc is in local 0
            var bytecode = new List<byte> { (byte)Opcode.Call };
            bytecode.Add((byte)DMReference.Type.Local);
            bytecode.Add(0); // Index 0
            bytecode.Add((byte)DMCallArgumentsType.None);
            bytecode.AddRange(BitConverter.GetBytes(0)); // 0 args
            bytecode.AddRange(BitConverter.GetBytes(0)); // unused
            bytecode.Add((byte)Opcode.Return);

            var mainProc = new DreamProc("main", bytecode.ToArray(), Array.Empty<string>(), 1);
            var thread = new DreamThread(mainProc, _vm.Context, 1000);

            // Push the proc to local 0
            thread.Push(new DreamValue(proc));
            // Stack: [proc]
            // Frame expects locals at stack base + args count + index.
            // main has 0 args, so local 0 is at stack base + 0.
            // thread constructor pushes a frame with stackBase 0.
            // So we need to push the local manually or use a proper call sequence.

            // Actually, thread.Push(proc) puts it at index 0.
            // The frame will look for it at stackBase + 0 + 0 = 0.

            thread.Run(1000);
            var result = thread.Pop();

            Assert.That(result.AsFloat(), Is.EqualTo(42f));
        }

        [Test]
        public void Output_StackBalance_Test()
        {
            // In DM: world << 123
            // Compiler: Push target, Push message, Output
            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushNull); // Target (world)
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(123f)); // Message
            bytecode.Add((byte)Opcode.Output);

            // Push something else to verify stack balance
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(456f));
            bytecode.Add((byte)Opcode.Return);

            var proc = new DreamProc("test", bytecode.ToArray(), Array.Empty<string>(), 0);
            var thread = new DreamThread(proc, _vm.Context, 1000);

            thread.Run(1000);

            Assert.That(thread.Pop().AsFloat(), Is.EqualTo(456f));
            Assert.That(thread.StackCount, Is.EqualTo(0));
        }

        [Test]
        public void DereferenceCall_FromVariable_Test()
        {
            var type = new ObjectType(1, "/test");
            type.VariableNames.Add("my_proc");
            _vm.Context.ObjectTypeManager = new ObjectTypeManager(NullLogger<ObjectTypeManager>.Instance);
            _vm.Context.ObjectTypeManager.RegisterObjectType(type);
            _vm.Context.Strings.Clear();
            _vm.Context.Strings.Add("my_proc");

            var procBytecode = new List<byte> { (byte)Opcode.PushFloat };
            procBytecode.AddRange(BitConverter.GetBytes(789f));
            procBytecode.Add((byte)Opcode.Return);
            var targetProc = new DreamProc("my_proc", procBytecode.ToArray(), Array.Empty<string>(), 0);

            var obj = new GameObject(type);
            obj.SetVariable("my_proc", new DreamValue(targetProc));

            // obj.my_proc()
            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushReferenceValue);
            bytecode.Add((byte)DMReference.Type.Src);
            bytecode.Add((byte)Opcode.DereferenceCall);
            bytecode.AddRange(BitConverter.GetBytes(0)); // stringId 0 ("my_proc")
            bytecode.Add((byte)DMCallArgumentsType.None);
            bytecode.AddRange(BitConverter.GetBytes(1)); // 1 stack delta (the object itself)
            bytecode.Add((byte)Opcode.Return);

            var mainProc = new DreamProc("main", bytecode.ToArray(), Array.Empty<string>(), 0);
            var thread = new DreamThread(mainProc, _vm.Context, 1000, obj);

            thread.Run(1000);
            var result = thread.Pop();

            Assert.That(result.AsFloat(), Is.EqualTo(789f));
        }

        [Test]
        public void Spawn_NullDelay_DoesNotCrash()
        {
            // spawn(null)
            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushNull); // delay
            bytecode.Add((byte)Opcode.Spawn);
            bytecode.AddRange(BitConverter.GetBytes(10)); // Jump address
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(42f));
            bytecode.Add((byte)Opcode.Return);
            bytecode.Add((byte)Opcode.Return);

            var proc = new DreamProc("test", bytecode.ToArray(), Array.Empty<string>(), 0);
            var thread = new DreamThread(proc, _vm.Context, 1000);

            Assert.DoesNotThrow(() => thread.Run(1000));
        }

        [Test]
        public void Length_NonContainer_ReturnsZero()
        {
            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(123.456f));
            bytecode.Add((byte)Opcode.Length);
            bytecode.Add((byte)Opcode.Return);

            var proc = new DreamProc("test", bytecode.ToArray(), Array.Empty<string>(), 0);
            var thread = new DreamThread(proc, _vm.Context, 1000);

            thread.Run(1000);
            Assert.That(thread.Pop().AsFloat(), Is.EqualTo(0f));
        }

        [Test]
        public void UsrWorldArgs_References_Test()
        {
            var worldType = new ObjectType(1, "/world");
            var mobType = new ObjectType(2, "/mob");

            var worldObj = new DreamObject(worldType);
            var usrObj = new DreamObject(mobType);

            _vm.Context.World = worldObj;

            // return list(world, usr, args.len)
            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushReferenceValue);
            bytecode.Add((byte)DMReference.Type.World);
            bytecode.Add((byte)Opcode.PushReferenceValue);
            bytecode.Add((byte)DMReference.Type.Usr);
            bytecode.Add((byte)Opcode.PushReferenceValue);
            bytecode.Add((byte)DMReference.Type.Args);
            bytecode.Add((byte)Opcode.Length); // args.len

            bytecode.Add((byte)Opcode.CreateList);
            bytecode.AddRange(BitConverter.GetBytes(3));
            bytecode.Add((byte)Opcode.Return);

            var proc = new DreamProc("test", bytecode.ToArray(), new[] { "arg1" }, 0);
            var thread = new DreamThread(proc, _vm.Context, 1000);
            thread.Usr = usrObj;
            thread.Push(new DreamValue(123f)); // arg1

            thread.Run(1000);
            var result = thread.Pop().GetValueAsDreamObject() as DreamList;

            Assert.That(result!.Values[0].GetValueAsDreamObject(), Is.EqualTo(worldObj));
            Assert.That(result.Values[1].GetValueAsDreamObject(), Is.EqualTo(usrObj));
            Assert.That(result.Values[2].AsFloat(), Is.EqualTo(1f));
        }

        [Test]
        public void Field_Increment_StackStability_Test()
        {
            var type = new ObjectType(1, "/test");
            type.VariableNames.Add("counter");
            type.FlattenedDefaultValues.Add(10f);

            var obj = new GameObject(type);
            _vm.Context.Strings.Clear();
            _vm.Context.Strings.Add("counter");

            // obj.counter++
            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushReferenceValue);
            bytecode.Add((byte)DMReference.Type.Src);
            bytecode.Add((byte)Opcode.Increment);
            bytecode.Add((byte)DMReference.Type.Field);
            bytecode.AddRange(BitConverter.GetBytes(0)); // stringId 0 ("counter")
            bytecode.Add((byte)Opcode.Return);

            var proc = new DreamProc("test", bytecode.ToArray(), Array.Empty<string>(), 0);
            var thread = new DreamThread(proc, _vm.Context, 1000, obj);

            thread.Run(1000);
            var result = thread.Pop();

            Assert.That(result.AsFloat(), Is.EqualTo(11f));
            Assert.That(obj.GetVariable("counter").AsFloat(), Is.EqualTo(11f));
            Assert.That(thread.StackCount, Is.EqualTo(0));
        }

        [Test]
        public void ListIndex_Assign_StackStability_Test()
        {
            var list = new DreamList(null);
            list.AddValue(new DreamValue(10f));

            // list[1] = 20
            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushReferenceValue);
            bytecode.Add((byte)DMReference.Type.Local);
            bytecode.Add(0); // list is in local 0

            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(1f)); // index 1

            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(20f)); // value 20

            bytecode.Add((byte)Opcode.Assign);
            bytecode.Add((byte)DMReference.Type.ListIndex);
            bytecode.Add((byte)Opcode.Return);

            var proc = new DreamProc("test", bytecode.ToArray(), Array.Empty<string>(), 1);
            var thread = new DreamThread(proc, _vm.Context, 1000);
            thread.Push(new DreamValue(list)); // local 0

            thread.Run(1000);
            var result = thread.Pop();

            Assert.That(result.AsFloat(), Is.EqualTo(20f));
            Assert.That(list.Values[0].AsFloat(), Is.EqualTo(20f));
            Assert.That(thread.StackCount, Is.EqualTo(0));
        }

        [Test]
        public void DereferenceCall_WithArguments_Mapping_Test()
        {
            var type = new ObjectType(1, "/test");
            _vm.Context.ObjectTypeManager = new ObjectTypeManager(NullLogger<ObjectTypeManager>.Instance);
            _vm.Context.ObjectTypeManager.RegisterObjectType(type);
            _vm.Context.Strings.Clear();
            _vm.Context.Strings.Add("my_proc");

            // my_proc(arg1, arg2) returns arg1 + arg2
            var procBytecode = new List<byte>();
            procBytecode.Add((byte)Opcode.PushReferenceValue);
            procBytecode.Add((byte)DMReference.Type.Argument);
            procBytecode.Add(0); // arg1
            procBytecode.Add((byte)Opcode.PushReferenceValue);
            procBytecode.Add((byte)DMReference.Type.Argument);
            procBytecode.Add(1); // arg2
            procBytecode.Add((byte)Opcode.Add);
            procBytecode.Add((byte)Opcode.Return);
            var targetProc = new DreamProc("my_proc", procBytecode.ToArray(), new[] { "arg1", "arg2" }, 0);
            type.Procs["my_proc"] = targetProc;

            var obj = new GameObject(type);

            // obj.my_proc(10, 20)
            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushReferenceValue);
            bytecode.Add((byte)DMReference.Type.Src); // Push obj
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(10f)); // Push arg1
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(20f)); // Push arg2
            bytecode.Add((byte)Opcode.DereferenceCall);
            bytecode.AddRange(BitConverter.GetBytes(0)); // stringId 0 ("my_proc")
            bytecode.Add((byte)DMCallArgumentsType.None);
            bytecode.AddRange(BitConverter.GetBytes(3)); // 3 stack delta (obj + 2 args)
            bytecode.Add((byte)Opcode.Return);

            var mainProc = new DreamProc("main", bytecode.ToArray(), Array.Empty<string>(), 0);
            var thread = new DreamThread(mainProc, _vm.Context, 1000, obj);

            thread.Run(1000);
            var result = thread.Pop();

            Assert.That(result.AsFloat(), Is.EqualTo(30f));
        }

        [Test]
        public void JumpIfTrueReference_Test()
        {
            var type = new ObjectType(1, "/test");
            type.VariableNames.Add("val");
            var obj = new GameObject(type);
            obj.SetVariable("val", 1f);

            _vm.Context.Strings.Clear();
            _vm.Context.Strings.Add("val");

            // if (obj.val) jump to 12
            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushReferenceValue);
            bytecode.Add((byte)DMReference.Type.Src);
            bytecode.Add((byte)Opcode.JumpIfTrueReference);
            bytecode.Add((byte)DMReference.Type.Field);
            bytecode.AddRange(BitConverter.GetBytes(0)); // counter
            bytecode.AddRange(BitConverter.GetBytes(18)); // jump address (12 + 1 + 4 + 1)

            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(0f)); // 12, 13, 14, 15, 16
            bytecode.Add((byte)Opcode.Return); // 17

            bytecode.Add((byte)Opcode.PushFloat); // 18
            bytecode.AddRange(BitConverter.GetBytes(1f)); // 19, 20, 21, 22
            bytecode.Add((byte)Opcode.Return); // 23

            var proc = new DreamProc("test", bytecode.ToArray(), Array.Empty<string>(), 0);
            var thread = new DreamThread(proc, _vm.Context, 1000, obj);

            thread.Run(1000);
            Assert.That(thread.Pop().AsFloat(), Is.EqualTo(1f));
            Assert.That(thread.StackCount, Is.EqualTo(0));
        }

        [Test]
        public void PopReference_Test()
        {
            var list = new DreamList(null);
            list.AddValue(new DreamValue(10f));

            // PopReference(list[1])
            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushReferenceValue);
            bytecode.Add((byte)DMReference.Type.Local);
            bytecode.Add(0); // list is in local 0

            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(1f)); // index 1

            bytecode.Add((byte)Opcode.PopReference);
            bytecode.Add((byte)DMReference.Type.ListIndex);

            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(42f));
            bytecode.Add((byte)Opcode.Return);

            var proc = new DreamProc("test", bytecode.ToArray(), Array.Empty<string>(), 1);
            var thread = new DreamThread(proc, _vm.Context, 1000);
            thread.Push(new DreamValue(list)); // local 0

            thread.Run(1000);
            Assert.That(thread.Pop().AsFloat(), Is.EqualTo(42f));
            Assert.That(thread.StackCount, Is.EqualTo(0));
        }

        [Test]
        public void TryCatch_Simple_Test()
        {
            // try { throw "error" } catch(var/e) { return e }
            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.Try);
            bytecode.AddRange(BitConverter.GetBytes(15)); // Catch address
            bytecode.Add((byte)DMReference.Type.Local);
            bytecode.Add(0); // catch variable in local 0

            bytecode.Add((byte)Opcode.PushString);
            bytecode.AddRange(BitConverter.GetBytes(0)); // string 0 ("error")
            bytecode.Add((byte)Opcode.Throw);

            bytecode.Add((byte)Opcode.EndTry);
            bytecode.Add((byte)Opcode.Return);

            // Catch block at 15
            bytecode.Add((byte)Opcode.PushReferenceValue);
            bytecode.Add((byte)DMReference.Type.Local);
            bytecode.Add(0);
            bytecode.Add((byte)Opcode.Return);

            _vm.Context.Strings.Clear();
            _vm.Context.Strings.Add("error");

            var proc = new DreamProc("test", bytecode.ToArray(), Array.Empty<string>(), 1);
            var thread = new DreamThread(proc, _vm.Context, 1000);

            thread.Run(1000);
            var result = thread.Pop();

            Assert.That(result.ToString(), Is.EqualTo("error"));
        }

        [Test]
        public void RangeView_Registration_Test()
        {
            var nativeProvider = new StandardNativeProcProvider();
            var nativeProcs = nativeProvider.GetNativeProcs();

            Assert.That(nativeProcs.ContainsKey("range"), Is.True);
            Assert.That(nativeProcs.ContainsKey("view"), Is.True);
        }

        [Test]
        public void IsSaved_Stub_Test()
        {
            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(123f));
            bytecode.Add((byte)Opcode.IsSaved);
            bytecode.Add((byte)Opcode.Return);

            var proc = new DreamProc("test", bytecode.ToArray(), Array.Empty<string>(), 0);
            var thread = new DreamThread(proc, _vm.Context, 1000);

            thread.Run(1000);
            Assert.That(thread.Pop(), Is.EqualTo(DreamValue.True));
        }
    }
}
