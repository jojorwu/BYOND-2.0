using Shared.Enums;
using Shared;
using NUnit.Framework;
using Core.VM.Runtime;
using Core.VM.Procs;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Services;

namespace tests
{
    [TestFixture]
    public class MathLogicTests
    {
        private DreamVM _vm = null!;

        [SetUp]
        public void SetUp()
        {
            _vm = new DreamVM(Options.Create(new DreamVmConfiguration()), NullLogger<DreamVM>.Instance, new INativeProcProvider[] {
                new MathNativeProcProvider(),
                new SpatialNativeProcProvider(),
                new SystemNativeProcProvider()
            }, MockDiagnosticBus.Instance);
        }

        [TearDown]
        public void TearDown()
        {
            _vm.Dispose();
        }

        [Test]
        public void ComplexMathExpression_Test()
        {
            // (10 + 20) * 2 / (5 - 3) ^ 2 = 30 * 2 / 2 ^ 2 = 60 / 4 = 15
            var bytecode = new List<byte>();

            // (10 + 20)
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(10.0));
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(20.0));
            bytecode.Add((byte)Opcode.Add);

            // * 2
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(2.0));
            bytecode.Add((byte)Opcode.Multiply);

            // (5 - 3)
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(5.0));
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(3.0));
            bytecode.Add((byte)Opcode.Subtract);

            // ^ 2
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(2.0));
            bytecode.Add((byte)Opcode.Power);

            // /
            bytecode.Add((byte)Opcode.Divide);
            bytecode.Add((byte)Opcode.Return);

            var proc = new DreamProc("test", bytecode.ToArray(), Array.Empty<string>(), 0, null, 0, 0);
            var thread = new DreamThread(proc, _vm.Context, 1000);

            thread.Run(1000);
            var result = thread.Pop();

            Assert.That(result.AsDouble(), Is.EqualTo(15.0).Within(0.00001));
        }

        [Test]
        public void Trigonometry_Test()
        {
            // sin(30) + cos(60) = 0.5 + 0.5 = 1.0
            var bytecode = new List<byte>();

            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(30.0));
            bytecode.Add((byte)Opcode.Sin);

            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(60.0));
            bytecode.Add((byte)Opcode.Cos);

            bytecode.Add((byte)Opcode.Add);
            bytecode.Add((byte)Opcode.Return);

            var proc = new DreamProc("test", bytecode.ToArray(), Array.Empty<string>(), 0, null, 0, 0);
            var thread = new DreamThread(proc, _vm.Context, 1000);

            thread.Run(1000);
            var result = thread.Pop();

            Assert.That(result.AsDouble(), Is.EqualTo(1.0).Within(0.00001));
        }

        [Test]
        public void BitwiseOperations_64Bit_Test()
        {
            // (1 << 40) | (1 << 20)
            var bytecode = new List<byte>();

            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(1.0));
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(40.0));
            bytecode.Add((byte)Opcode.BitShiftLeft);

            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(1.0));
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(20.0));
            bytecode.Add((byte)Opcode.BitShiftLeft);

            bytecode.Add((byte)Opcode.BitOr);
            bytecode.Add((byte)Opcode.Return);

            var proc = new DreamProc("test", bytecode.ToArray(), Array.Empty<string>(), 0, null, 0, 0);
            var thread = new DreamThread(proc, _vm.Context, 1000);

            thread.Run(1000);
            var result = thread.Pop();

            long expected = (1L << 40) | (1L << 20);
            Assert.That(result.RawLong, Is.EqualTo(expected));
        }

        [Test]
        public void IndexCalculation_Test()
        {
            // list(1, 2, 3, 4, 5)[2 + 1] -> 3
            var bytecode = new List<byte>();

            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(1.0));
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(2.0));
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(3.0));
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(4.0));
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(5.0));

            bytecode.Add((byte)Opcode.CreateList);
            bytecode.AddRange(BitConverter.GetBytes(5));

            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(2.0));
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(1.0));
            bytecode.Add((byte)Opcode.Add);

            bytecode.Add((byte)Opcode.DereferenceIndex);
            bytecode.Add((byte)Opcode.Return);

            _vm.Context.ListType = new ObjectType(0, "/list");
            var proc = new DreamProc("test", bytecode.ToArray(), Array.Empty<string>(), 0, null, 0, 0);
            var thread = new DreamThread(proc, _vm.Context, 1000);

            thread.Run(1000);
            var result = thread.Pop();

            Assert.That(result.AsDouble(), Is.EqualTo(3.0));
        }
    }
}
