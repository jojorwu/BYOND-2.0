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

namespace tests
{
    [TestFixture]
    public class DreamVMTests
    {
        private DreamVM _vm = null!;

        [SetUp]
        public void SetUp()
        {
            _vm = new DreamVM(Options.Create(new ServerSettings()), NullLogger<DreamVM>.Instance, new INativeProcProvider[] { new Core.VM.Procs.StandardNativeProcProvider() });
        }

        private DreamValue RunTest(params object[] ops)
        {
            var bytecode = new List<byte>();
            foreach (var op in ops)
            {
                if (op is Opcode opcode) bytecode.Add((byte)opcode);
                else if (op is float f) bytecode.AddRange(BitConverter.GetBytes(f));
                else if (op is int i) bytecode.AddRange(BitConverter.GetBytes(i));
                else if (op is byte b) bytecode.Add(b);
                else throw new ArgumentException($"Unsupported op type: {op.GetType()}");
            }

            var proc = new DreamProc(string.Empty, bytecode.ToArray(), Array.Empty<string>(), 0);
            var thread = new DreamThread(proc, _vm.Context, 1000);
            thread.Run(1000);
            return thread.Peek();
        }

        [Test]
        [TestCase(5, 3, 1)]
        [TestCase(10, 6, 2)]
        public void BitAnd_PerformsCorrectOperation(float a, float b, float expected)
        {
            var result = RunTest(Opcode.PushFloat, a, Opcode.PushFloat, b, Opcode.BitAnd, Opcode.Return);
            Assert.That(result.AsFloat(), Is.EqualTo(expected));
        }

        [Test]
        [TestCase(5, 3, 7)]
        [TestCase(10, 6, 14)]
        public void BitOr_PerformsCorrectOperation(float a, float b, float expected)
        {
            var result = RunTest(Opcode.PushFloat, a, Opcode.PushFloat, b, Opcode.BitOr, Opcode.Return);
            Assert.That(result.AsFloat(), Is.EqualTo(expected));
        }

        [Test]
        [TestCase(5, 3, 6)]
        [TestCase(10, 6, 12)]
        public void BitXor_PerformsCorrectOperation(float a, float b, float expected)
        {
            var result = RunTest(Opcode.PushFloat, a, Opcode.PushFloat, b, Opcode.BitXor, Opcode.Return);
            Assert.That(result.AsFloat(), Is.EqualTo(expected));
        }

        [Test]
        public void BitNot_PerformsCorrectOperation()
        {
            var result = RunTest(Opcode.PushFloat, 5f, Opcode.BitNot, Opcode.Return);
            Assert.That(result.AsFloat(), Is.EqualTo(~5));
        }

        [Test]
        [TestCase(5, 2, 20)]
        [TestCase(10, 3, 80)]
        public void BitShiftLeft_PerformsCorrectOperation(float a, float b, float expected)
        {
            var result = RunTest(Opcode.PushFloat, a, Opcode.PushFloat, b, Opcode.BitShiftLeft, Opcode.Return);
            Assert.That(result.AsFloat(), Is.EqualTo(expected));
        }

        [Test]
        [TestCase(20, 2, 5)]
        [TestCase(80, 3, 10)]
        public void BitShiftRight_PerformsCorrectOperation(float a, float b, float expected)
        {
            var result = RunTest(Opcode.PushFloat, a, Opcode.PushFloat, b, Opcode.BitShiftRight, Opcode.Return);
            Assert.That(result.AsFloat(), Is.EqualTo(expected));
        }

        [Test]
        [TestCase(5, 10, 1)]
        [TestCase(10, 5, 0)]
        [TestCase(5, 5, 0)]
        public void CompareLessThan_PerformsCorrectOperation(float a, float b, float expected)
        {
            var result = RunTest(Opcode.PushFloat, a, Opcode.PushFloat, b, Opcode.CompareLessThan, Opcode.Return);
            Assert.That(result.AsFloat(), Is.EqualTo(expected));
        }

        [Test]
        [TestCase(10, 5, 1)]
        [TestCase(5, 10, 0)]
        [TestCase(5, 5, 0)]
        public void CompareGreaterThan_PerformsCorrectOperation(float a, float b, float expected)
        {
            var result = RunTest(Opcode.PushFloat, a, Opcode.PushFloat, b, Opcode.CompareGreaterThan, Opcode.Return);
            Assert.That(result.AsFloat(), Is.EqualTo(expected));
        }

        [Test]
        [TestCase(5, 10, 1)]
        [TestCase(10, 5, 0)]
        [TestCase(5, 5, 1)]
        public void CompareLessThanOrEqual_PerformsCorrectOperation(float a, float b, float expected)
        {
            var result = RunTest(Opcode.PushFloat, a, Opcode.PushFloat, b, Opcode.CompareLessThanOrEqual, Opcode.Return);
            Assert.That(result.AsFloat(), Is.EqualTo(expected));
        }

        [Test]
        [TestCase(10, 5, 1)]
        [TestCase(5, 10, 0)]
        [TestCase(5, 5, 1)]
        public void CompareGreaterThanOrEqual_PerformsCorrectOperation(float a, float b, float expected)
        {
            var result = RunTest(Opcode.PushFloat, a, Opcode.PushFloat, b, Opcode.CompareGreaterThanOrEqual, Opcode.Return);
            Assert.That(result.AsFloat(), Is.EqualTo(expected));
        }

        [Test]
        [TestCase(1, 0)]
        [TestCase(0, 1)]
        [TestCase(5, 0)]
        public void BooleanNot_PerformsCorrectOperation(float a, float expected)
        {
            var result = RunTest(Opcode.PushFloat, a, Opcode.BooleanNot, Opcode.Return);
            Assert.That(result.AsFloat(), Is.EqualTo(expected));
        }

        [Test]
        public void Negate_PerformsCorrectOperation()
        {
            var result = RunTest(Opcode.PushFloat, 5f, Opcode.Negate, Opcode.Return);
            Assert.That(result.AsFloat(), Is.EqualTo(-5f));
        }

        [Test]
        public void Math_Sqrt_Works()
        {
            var result = RunTest(Opcode.PushFloat, 16f, Opcode.Sqrt, Opcode.Return);
            Assert.That(result.AsFloat(), Is.EqualTo(4f));
        }

        [Test]
        public void Math_Abs_Works()
        {
            var result = RunTest(Opcode.PushFloat, -10f, Opcode.Abs, Opcode.Return);
            Assert.That(result.AsFloat(), Is.EqualTo(10f));
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
            var thread = new DreamThread(proc, _vm.Context, 1000);
            thread.Run(1000);

            Assert.That(thread.StackCount, Is.EqualTo(1));
            Assert.That(thread.Peek().AsFloat(), Is.EqualTo(10f));
        }
    }
}
