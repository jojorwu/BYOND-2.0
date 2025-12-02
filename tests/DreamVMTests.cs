using NUnit.Framework;
using Core.VM;
using Core;
using System.Collections.Generic;
using Core.VM.Runtime;
using Core.VM.Procs;
using Core.VM.Types;
using Core.VM.Opcodes;
using System;
using System.Linq;

namespace tests
{
    [TestFixture]
    public class DreamVMTests
    {
        private DreamVM _vm = null!;

        [SetUp]
        public void SetUp()
        {
            _vm = new DreamVM(new ServerSettings());
        }

        private void TestComparison(Opcode opcode, DreamValue a, DreamValue b, bool expected)
        {
            var bytecode = new[] { (byte)opcode };
            var proc = new DreamProc("main", bytecode, Array.Empty<string>(), 0);
            var thread = new DreamThread(proc, _vm, 1000);

            thread.Push(a);
            thread.Push(b);

            thread.Run(100);

            Assert.That(thread.Stack.Count, Is.EqualTo(1));
            Assert.That(thread.Stack[0].TryGetValue(out float resultValue), Is.True);
            Assert.That(resultValue == 1.0f, Is.EqualTo(expected));
        }

        private DreamValue RunTest(byte[] bytecode)
        {
            var proc = new DreamProc(string.Empty, bytecode, Array.Empty<string>(), 0);
            var thread = new DreamThread(proc, _vm, 1000);
            thread.Run(1000);
            return thread.Stack.Last();
        }

        [Test]
        [TestCase(5, 3, 1)]     // 0101 & 0011 = 0001
        [TestCase(10, 6, 2)]    // 1010 & 0110 = 0010
        public void BitAnd_PerformsCorrectOperation(float a, float b, float expected)
        {
            var bytecode = new List<byte>
            {
                (byte)Opcode.PushFloat
            };
            bytecode.AddRange(BitConverter.GetBytes(a));
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(b));
            bytecode.Add((byte)Opcode.BitAnd);

            var result = RunTest(bytecode.ToArray());
            Assert.That(result.TryGetValue(out float value), Is.True);
            Assert.That(value, Is.EqualTo(expected));
        }

        [Test]
        [TestCase(5, 3, 7)]     // 0101 | 0011 = 0111
        [TestCase(10, 6, 14)]   // 1010 | 0110 = 1110
        public void BitOr_PerformsCorrectOperation(float a, float b, float expected)
        {
            var bytecode = new List<byte>
            {
                (byte)Opcode.PushFloat
            };
            bytecode.AddRange(BitConverter.GetBytes(a));
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(b));
            bytecode.Add((byte)Opcode.BitOr);

            var result = RunTest(bytecode.ToArray());
            Assert.That(result.TryGetValue(out float value), Is.True);
            Assert.That(value, Is.EqualTo(expected));
        }

        [Test]
        [TestCase(5, 3, 6)]     // 0101 ^ 0011 = 0110
        [TestCase(10, 6, 12)]   // 1010 ^ 0110 = 1100
        public void BitXor_PerformsCorrectOperation(float a, float b, float expected)
        {
            var bytecode = new List<byte>
            {
                (byte)Opcode.PushFloat
            };
            bytecode.AddRange(BitConverter.GetBytes(a));
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(b));
            bytecode.Add((byte)Opcode.BitXor);

            var result = RunTest(bytecode.ToArray());
            Assert.That(result.TryGetValue(out float value), Is.True);
            Assert.That(value, Is.EqualTo(expected));
        }

        [Test]
        public void BitNot_PerformsCorrectOperation()
        {
            var bytecode = new List<byte>
            {
                (byte)Opcode.PushFloat
            };
            bytecode.AddRange(BitConverter.GetBytes(5f));
            bytecode.Add((byte)Opcode.BitNot);

            var result = RunTest(bytecode.ToArray());
            Assert.That(result.TryGetValue(out float value), Is.True);
            Assert.That(value, Is.EqualTo(~5));
        }

        [Test]
        [TestCase(5, 2, 20)]     // 0101 << 2 = 10100
        [TestCase(10, 3, 80)]   // 1010 << 3 = 101000
        public void BitShiftLeft_PerformsCorrectOperation(float a, float b, float expected)
        {
            var bytecode = new List<byte>
            {
                (byte)Opcode.PushFloat
            };
            bytecode.AddRange(BitConverter.GetBytes(a));
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(b));
            bytecode.Add((byte)Opcode.BitShiftLeft);

            var result = RunTest(bytecode.ToArray());
            Assert.That(result.TryGetValue(out float value), Is.True);
            Assert.That(value, Is.EqualTo(expected));
        }

        [Test]
        [TestCase(20, 2, 5)]     // 10100 >> 2 = 0101
        [TestCase(80, 3, 10)]   // 101000 >> 3 = 1010
        public void BitShiftRight_PerformsCorrectOperation(float a, float b, float expected)
        {
            var bytecode = new List<byte>
            {
                (byte)Opcode.PushFloat
            };
            bytecode.AddRange(BitConverter.GetBytes(a));
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(b));
            bytecode.Add((byte)Opcode.BitShiftRight);

            var result = RunTest(bytecode.ToArray());
            Assert.That(result.TryGetValue(out float value), Is.True);
            Assert.That(value, Is.EqualTo(expected));
        }

        [Test]
        [TestCase(5, 10, 1)] // 5 < 10
        [TestCase(10, 5, 0)] // 10 not < 5
        [TestCase(5, 5, 0)]  // 5 not < 5
        public void CompareLessThan_PerformsCorrectOperation(float a, float b, float expected)
        {
            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(a));
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(b));
            bytecode.Add((byte)Opcode.CompareLessThan);

            var result = RunTest(bytecode.ToArray());
            Assert.That(result.TryGetValue(out float value), Is.True);
            Assert.That(value, Is.EqualTo(expected));
        }

        [Test]
        [TestCase(10, 5, 1)] // 10 > 5
        [TestCase(5, 10, 0)] // 5 not > 10
        [TestCase(5, 5, 0)]  // 5 not > 5
        public void CompareGreaterThan_PerformsCorrectOperation(float a, float b, float expected)
        {
            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(a));
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(b));
            bytecode.Add((byte)Opcode.CompareGreaterThan);

            var result = RunTest(bytecode.ToArray());
            Assert.That(result.TryGetValue(out float value), Is.True);
            Assert.That(value, Is.EqualTo(expected));
        }

        [Test]
        [TestCase(5, 10, 1)] // 5 <= 10
        [TestCase(10, 5, 0)] // 10 not <= 5
        [TestCase(5, 5, 1)]  // 5 <= 5
        public void CompareLessThanOrEqual_PerformsCorrectOperation(float a, float b, float expected)
        {
            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(a));
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(b));
            bytecode.Add((byte)Opcode.CompareLessThanOrEqual);

            var result = RunTest(bytecode.ToArray());
            Assert.That(result.TryGetValue(out float value), Is.True);
            Assert.That(value, Is.EqualTo(expected));
        }

        [Test]
        [TestCase(10, 5, 1)] // 10 >= 5
        [TestCase(5, 10, 0)] // 5 not >= 10
        [TestCase(5, 5, 1)]  // 5 >= 5
        public void CompareGreaterThanOrEqual_PerformsCorrectOperation(float a, float b, float expected)
        {
            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(a));
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(b));
            bytecode.Add((byte)Opcode.CompareGreaterThanOrEqual);

            var result = RunTest(bytecode.ToArray());
            Assert.That(result.TryGetValue(out float value), Is.True);
            Assert.That(value, Is.EqualTo(expected));
        }

        [Test]
        public void Negate_PerformsCorrectOperation()
        {
            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(5f));
            bytecode.Add((byte)Opcode.Negate);

            var result = RunTest(bytecode.ToArray());
            Assert.That(result.TryGetValue(out float value), Is.True);
            Assert.That(value, Is.EqualTo(-5f));
        }

        [Test]
        [TestCase(1, 0)] // !1 = 0
        [TestCase(0, 1)] // !0 = 1
        [TestCase(5, 0)] // !5 = 0
        public void BooleanNot_PerformsCorrectOperation(float a, float expected)
        {
            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(a));
            bytecode.Add((byte)Opcode.BooleanNot);

            var result = RunTest(bytecode.ToArray());
            Assert.That(result.TryGetValue(out float value), Is.True);
            Assert.That(value, Is.EqualTo(expected));
        }

        [Test]
        public void PushNull_PushesNullOntoStack()
        {
            var bytecode = new byte[] { (byte)Opcode.PushNull };
            var result = RunTest(bytecode);
            Assert.That(result.Type, Is.EqualTo(DreamValueType.Null));
        }

        [Test]
        public void Pop_RemovesTopValueFromStack()
        {
            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(10f));
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(20f));
            bytecode.Add((byte)Opcode.Pop);

            var proc = new DreamProc(string.Empty, bytecode.ToArray(), Array.Empty<string>(), 0);
            var thread = new DreamThread(proc, _vm, 1000);
            thread.Run(1000);

            Assert.That(thread.Stack.Count, Is.EqualTo(1));
            Assert.That(thread.Stack.Last().TryGetValue(out float value), Is.True);
            Assert.That(value, Is.EqualTo(10f));
        }

        [Test]
        public void CallAndReturn_WithArgumentsAndLocals_WorksCorrectly()
        {
            // main proc
            var mainBytecode = new List<byte>();
            mainBytecode.Add((byte)Opcode.PushFloat); // Push arg1 for add proc
            mainBytecode.AddRange(BitConverter.GetBytes(10f));
            mainBytecode.Add((byte)Opcode.PushFloat); // Push arg2 for add proc
            mainBytecode.AddRange(BitConverter.GetBytes(5f));
            mainBytecode.Add((byte)Opcode.Call);
            mainBytecode.AddRange(BitConverter.GetBytes(0)); // String ID for "add"
            mainBytecode.Add(2); // Arg count
            mainBytecode.Add((byte)Opcode.Return); // Return from main

            // add proc (arg1, arg2)
            var addBytecode = new List<byte>();
            addBytecode.Add((byte)Opcode.PushArgument);
            addBytecode.Add(0); // Push arg1
            addBytecode.Add((byte)Opcode.PushArgument);
            addBytecode.Add(1); // Push arg2
            addBytecode.Add((byte)Opcode.Add);
            addBytecode.Add((byte)Opcode.Return); // Return sum

            var mainProc = new DreamProc("main", mainBytecode.ToArray(), Array.Empty<string>(), 0);
            var addProc = new DreamProc("add", addBytecode.ToArray(), new string[] { "arg1", "arg2" }, 0);
            _vm.Procs["main"] = mainProc;
            _vm.Procs["add"] = addProc;
            _vm.Strings.Add("add"); // String ID 0 is "add"

            var thread = new DreamThread(mainProc, _vm, 1000);
            thread.Run(1000);

            Assert.That(thread.State, Is.EqualTo(DreamThreadState.Finished));
            Assert.That(thread.Stack.Count, Is.EqualTo(1));
            Assert.That(thread.Stack[0].TryGetValue(out float returnValue), Is.True);
            Assert.That(returnValue, Is.EqualTo(15f));
        }

        [Test]
        public void Initial_GetsDefaultPropertyValue()
        {
            // Arrange
            var objectType = new ObjectType("test_object");
            objectType.DefaultProperties["my_prop"] = 42f;
            var gameObject = new GameObject(objectType);
            var dreamObject = new DreamObject(gameObject);

            _vm.Strings.Add("my_prop"); // String ID 0

            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.Initial);
            bytecode.AddRange(BitConverter.GetBytes(0)); // String ID for "my_prop"

            var proc = new DreamProc("main", bytecode.ToArray(), Array.Empty<string>(), 0);
            var thread = new DreamThread(proc, _vm, 1000);
            thread.Push(new DreamValue(dreamObject));

            // Act
            thread.Run(1000);

            // Assert
            Assert.That(thread.State, Is.EqualTo(DreamThreadState.Finished));
            Assert.That(thread.Stack.Count, Is.EqualTo(1));
            Assert.That(thread.Stack[0].TryGetValue(out float resultValue), Is.True);
            Assert.That(resultValue, Is.EqualTo(42f));
        }

        [Test]
        public void CompareEquivalent_HandlesAllTypes()
        {
            // Nulls are equivalent
            TestComparison(Opcode.CompareEquivalent, DreamValue.Null, DreamValue.Null, true);

            // Numbers are equivalent by value
            TestComparison(Opcode.CompareEquivalent, new DreamValue(5), new DreamValue(5), true);
            TestComparison(Opcode.CompareEquivalent, new DreamValue(5), new DreamValue(10), false);

            // Strings are equivalent by value
            TestComparison(Opcode.CompareEquivalent, new DreamValue("hello"), new DreamValue("hello"), true);
            TestComparison(Opcode.CompareEquivalent, new DreamValue("hello"), new DreamValue("world"), false);

            // Objects are equivalent by type
            var parentType = new ObjectType("parent");
            var childType = new ObjectType("child", parentType);
            _vm.ObjectTypeManager.ObjectTypes.TryAdd(parentType.Name, parentType);
            _vm.ObjectTypeManager.ObjectTypes.TryAdd(childType.Name, childType);

            var objA = new DreamObject(new GameObject(parentType));
            var objB = new DreamObject(new GameObject(parentType));
            var objC = new DreamObject(new GameObject(childType));

            TestComparison(Opcode.CompareEquivalent, new DreamValue(objA), new DreamValue(objB), true);
            TestComparison(Opcode.CompareEquivalent, new DreamValue(objA), new DreamValue(objC), true);
        }

        [Test]
        public void CompareNotEquivalent_HandlesAllTypes()
        {
            // Nulls are equivalent
            TestComparison(Opcode.CompareNotEquivalent, DreamValue.Null, DreamValue.Null, false);

            // Numbers are equivalent by value
            TestComparison(Opcode.CompareNotEquivalent, new DreamValue(5), new DreamValue(5), false);
            TestComparison(Opcode.CompareNotEquivalent, new DreamValue(5), new DreamValue(10), true);

            // Strings are equivalent by value
            TestComparison(Opcode.CompareNotEquivalent, new DreamValue("hello"), new DreamValue("hello"), false);
            TestComparison(Opcode.CompareNotEquivalent, new DreamValue("hello"), new DreamValue("world"), true);

            // Objects are equivalent by type
            var parentType = new ObjectType("parent2");
            var childType = new ObjectType("child2", parentType);
            _vm.ObjectTypeManager.ObjectTypes.TryAdd(parentType.Name, parentType);
            _vm.ObjectTypeManager.ObjectTypes.TryAdd(childType.Name, childType);

            var objA = new DreamObject(new GameObject(parentType));
            var objB = new DreamObject(new GameObject(parentType));
            var objC = new DreamObject(new GameObject(childType));

            TestComparison(Opcode.CompareNotEquivalent, new DreamValue(objA), new DreamValue(objB), false);
            TestComparison(Opcode.CompareNotEquivalent, new DreamValue(objA), new DreamValue(objC), false);
        }
    }
}
