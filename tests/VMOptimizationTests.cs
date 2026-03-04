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
            // 0: PushReferenceValue(Local, 0) (6 bytes)
            // 6: PushReferenceValue(Local, 1) (6 bytes)
            // 12: Jump(17) (5 bytes)
            // 17: Add (1 byte) -> Target

            // Optimized:
            // 0: PushLocal(0) (5 bytes) -> -1 byte
            // 5: PushLocal(1) (5 bytes) -> -1 byte
            // 10: Jump(15) (5 bytes)
            // 15: Add (1 byte) -> Target

            byte[] bytecode = new byte[18];
            bytecode[0] = (byte)Opcode.PushReferenceValue;
            bytecode[1] = (byte)DMReference.Type.Local;
            BitConverter.TryWriteBytes(bytecode.AsSpan(2), 0);

            bytecode[6] = (byte)Opcode.PushReferenceValue;
            bytecode[7] = (byte)DMReference.Type.Local;
            BitConverter.TryWriteBytes(bytecode.AsSpan(8), 1);

            bytecode[12] = (byte)Opcode.Jump;
            BitConverter.TryWriteBytes(bytecode.AsSpan(13), 17);

            bytecode[17] = (byte)Opcode.Add;

            byte[] optimized = BytecodeOptimizer.Optimize(bytecode);

            // Verify size reduction: 6+6+5+1 = 18 -> 5+5+5+1 = 16
            Assert.That(optimized.Length, Is.EqualTo(16));
            Assert.That(optimized[0], Is.EqualTo((byte)Opcode.PushLocal));
            Assert.That(optimized[5], Is.EqualTo((byte)Opcode.PushLocal));
            Assert.That(optimized[10], Is.EqualTo((byte)Opcode.Jump));

            int target = BitConverter.ToInt32(optimized, 11);
            Assert.That(target, Is.EqualTo(15)); // Target should be updated from 17 to 15
        }

        [Test]
        public void BytecodeOptimizer_HandlesSuperInstructionsWithJumps()
        {
            // Pattern:
            // 0: PushReferenceValue(Local, 0) (6 bytes)
            // 6: PushReferenceValue(Local, 1) (6 bytes)
            // 12: Add (1 byte)
            // 13: Jump(0) (5 bytes)

            // Optimized:
            // 0: LocalPushLocalPushAdd(0, 1) (9 bytes) -> -4 bytes
            // 9: Jump(0) (5 bytes)

            byte[] bytecode = new byte[18];
            bytecode[0] = (byte)Opcode.PushReferenceValue;
            bytecode[1] = (byte)DMReference.Type.Local;
            BitConverter.TryWriteBytes(bytecode.AsSpan(2), 0);

            bytecode[6] = (byte)Opcode.PushReferenceValue;
            bytecode[7] = (byte)DMReference.Type.Local;
            BitConverter.TryWriteBytes(bytecode.AsSpan(8), 1);

            bytecode[12] = (byte)Opcode.Add;
            bytecode[13] = (byte)Opcode.Jump;
            BitConverter.TryWriteBytes(bytecode.AsSpan(14), 0);

            byte[] optimized = BytecodeOptimizer.Optimize(bytecode);

            Assert.That(optimized.Length, Is.EqualTo(14));
            Assert.That(optimized[0], Is.EqualTo((byte)Opcode.LocalPushLocalPushAdd));
            Assert.That(optimized[9], Is.EqualTo((byte)Opcode.Jump));

            int target = BitConverter.ToInt32(optimized, 10);
            Assert.That(target, Is.EqualTo(0));
        }

        [Test]
        public void BytecodeOptimizer_HandlesVariableArgsCorrectly()
        {
            // PushNRefs(1) followed by Local reference
            // Opcode(1) + Count(4) + RefType(1) + Idx(4) = 10 bytes
            byte[] bytecode = new byte[11];
            bytecode[0] = (byte)Opcode.PushNRefs;
            BitConverter.TryWriteBytes(bytecode.AsSpan(1), 1);
            bytecode[5] = (byte)DMReference.Type.Local;
            BitConverter.TryWriteBytes(bytecode.AsSpan(6), 5);
            bytecode[10] = (byte)Opcode.Return;

            // It should NOT try to optimize the Local reference inside PushNRefs
            byte[] optimized = BytecodeOptimizer.Optimize(bytecode);

            Assert.That(optimized.Length, Is.EqualTo(11));
            Assert.That(optimized[0], Is.EqualTo((byte)Opcode.PushNRefs));
            Assert.That(optimized[5], Is.EqualTo((byte)DMReference.Type.Local));
            Assert.That(BitConverter.ToInt32(optimized, 6), Is.EqualTo(5));
        }
    }
}
