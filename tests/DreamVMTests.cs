using Shared;
using NUnit.Framework;
using Core.VM;
using Core;
using System.Collections.Generic;
using Core.VM.Runtime;
using Core.VM.Procs;
using Core.VM.Types;
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
            bytecode.Add((byte)Opcode.Return);

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
            bytecode.Add((byte)Opcode.Return);

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
            bytecode.Add((byte)Opcode.Return);

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
            bytecode.Add((byte)Opcode.Return);

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
            bytecode.Add((byte)Opcode.Return);

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
            bytecode.Add((byte)Opcode.Return);

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
            bytecode.Add((byte)Opcode.Return);

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
            bytecode.Add((byte)Opcode.Return);

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
            bytecode.Add((byte)Opcode.Return);

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
            bytecode.Add((byte)Opcode.Return);

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
            bytecode.Add((byte)Opcode.Return);

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
            bytecode.Add((byte)Opcode.Return);

            var result = RunTest(bytecode.ToArray());
            Assert.That(result.TryGetValue(out float value), Is.True);
            Assert.That(value, Is.EqualTo(expected));
        }

        [Test]
        public void PushNull_PushesNullOntoStack()
        {
            var bytecode = new byte[] { (byte)Opcode.PushNull, (byte)Opcode.Return };
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
            bytecode.Add((byte)Opcode.Return);

            var proc = new DreamProc(string.Empty, bytecode.ToArray(), Array.Empty<string>(), 0);
            var thread = new DreamThread(proc, _vm, 1000);
            thread.Run(1000);

            Assert.That(thread.Stack.Count, Is.EqualTo(1));
            Assert.That(thread.Stack.Last().TryGetValue(out float value), Is.True);
            Assert.That(value, Is.EqualTo(10f));
        }

    }
}
