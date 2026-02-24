using Shared.Enums;
using NUnit.Framework;
using Core.VM.Utils;
using Shared;
using System;
using System.Collections.Generic;

namespace tests
{
    [TestFixture]
    public class VMOptimizationTests
    {
        [Test]
        public void BytecodeOptimizer_AdjustsJumpOffsets()
        {
            // Pattern:
            // 0: PushReferenceValue(Local, 0) (3 bytes)
            // 3: PushReferenceValue(Local, 1) (3 bytes)
            // 6: Jump(11) (5 bytes)
            // 11: Add (1 byte) -> Target

            // Optimized:
            // 0: PushLocal(0) (2 bytes) -> -1 byte
            // 2: PushLocal(1) (2 bytes) -> -1 byte
            // 4: Jump(9) (5 bytes)
            // 9: Add (1 byte) -> Target

            byte[] bytecode = new byte[] {
                (byte)Opcode.PushReferenceValue, (byte)DMReference.Type.Local, 0,
                (byte)Opcode.PushReferenceValue, (byte)DMReference.Type.Local, 1,
                (byte)Opcode.Jump, 11, 0, 0, 0,
                (byte)Opcode.Add
            };

            byte[] optimized = BytecodeOptimizer.Optimize(bytecode);

            // Verify size reduction: 3+3+5+1 = 12 -> 2+2+5+1 = 10
            Assert.That(optimized.Length, Is.EqualTo(10));
            Assert.That(optimized[0], Is.EqualTo((byte)Opcode.PushLocal));
            Assert.That(optimized[2], Is.EqualTo((byte)Opcode.PushLocal));
            Assert.That(optimized[4], Is.EqualTo((byte)Opcode.Jump));

            int target = BitConverter.ToInt32(optimized, 5);
            Assert.That(target, Is.EqualTo(9)); // Target should be updated from 11 to 9
        }

        [Test]
        public void BytecodeOptimizer_HandlesSuperInstructionsWithJumps()
        {
            // Pattern:
            // 0: PushReferenceValue(Local, 0) (3 bytes)
            // 3: PushReferenceValue(Local, 1) (3 bytes)
            // 6: Add (1 byte)
            // 7: Jump(0) (5 bytes)

            // Optimized:
            // 0: LocalPushLocalPushAdd(0, 1) (3 bytes) -> -4 bytes
            // 3: Jump(0) (5 bytes)

            byte[] bytecode = new byte[] {
                (byte)Opcode.PushReferenceValue, (byte)DMReference.Type.Local, 0,
                (byte)Opcode.PushReferenceValue, (byte)DMReference.Type.Local, 1,
                (byte)Opcode.Add,
                (byte)Opcode.Jump, 0, 0, 0, 0
            };

            byte[] optimized = BytecodeOptimizer.Optimize(bytecode);

            Assert.That(optimized.Length, Is.EqualTo(8));
            Assert.That(optimized[0], Is.EqualTo((byte)Opcode.LocalPushLocalPushAdd));
            Assert.That(optimized[3], Is.EqualTo((byte)Opcode.Jump));

            int target = BitConverter.ToInt32(optimized, 4);
            Assert.That(target, Is.EqualTo(0));
        }

        [Test]
        public void BytecodeOptimizer_HandlesVariableArgsCorrectly()
        {
            // PushNRefs(1) followed by Local reference
            // Opcode(1) + Count(4) + RefType(1) + Idx(1) = 7 bytes
            byte[] bytecode = new byte[] {
                (byte)Opcode.PushNRefs, 1, 0, 0, 0,
                (byte)DMReference.Type.Local, 5,
                (byte)Opcode.Return
            };

            // It should NOT try to optimize the Local reference inside PushNRefs
            byte[] optimized = BytecodeOptimizer.Optimize(bytecode);

            Assert.That(optimized.Length, Is.EqualTo(8));
            Assert.That(optimized[0], Is.EqualTo((byte)Opcode.PushNRefs));
            Assert.That(optimized[5], Is.EqualTo((byte)DMReference.Type.Local));
            Assert.That(optimized[6], Is.EqualTo(5));
        }
    }
}
