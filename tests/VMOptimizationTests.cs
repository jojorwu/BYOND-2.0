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

            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushReferenceValue);
            bytecode.Add((byte)DMReference.Type.Local);
            bytecode.AddRange(BitConverter.GetBytes(0)); // 6 bytes
            bytecode.Add((byte)Opcode.PushReferenceValue);
            bytecode.Add((byte)DMReference.Type.Local);
            bytecode.AddRange(BitConverter.GetBytes(1)); // 6 bytes
            bytecode.Add((byte)Opcode.Jump);
            bytecode.AddRange(BitConverter.GetBytes(17)); // 5 bytes. Target is at 17.
            bytecode.Add((byte)Opcode.Add); // PC 17

            byte[] optimized = BytecodeOptimizer.Optimize(bytecode.ToArray());

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
            // 0: PushReferenceValue(Local, 0) (3 bytes)
            // 3: PushReferenceValue(Local, 1) (3 bytes)
            // 6: Add (1 byte)
            // 7: Jump(0) (5 bytes)

            // Optimized:
            // 0: LocalPushLocalPushAdd(0, 1) (3 bytes) -> -4 bytes
            // 3: Jump(0) (5 bytes)

            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushReferenceValue);
            bytecode.Add((byte)DMReference.Type.Local);
            bytecode.AddRange(BitConverter.GetBytes(0));
            bytecode.Add((byte)Opcode.PushReferenceValue);
            bytecode.Add((byte)DMReference.Type.Local);
            bytecode.AddRange(BitConverter.GetBytes(1));
            bytecode.Add((byte)Opcode.Add);
            bytecode.Add((byte)Opcode.Jump);
            bytecode.AddRange(BitConverter.GetBytes(0));

            byte[] optimized = BytecodeOptimizer.Optimize(bytecode.ToArray());

            // 1 (Opcode) + 4 (idx1) + 4 (idx2) = 9 bytes
            // + 1 (Jump) + 4 (target) = 14 bytes
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
            // Opcode(1) + Count(4) + [RefType(1) + Idx(4)] = 10 bytes
            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushNRefs);
            bytecode.AddRange(BitConverter.GetBytes(1));
            bytecode.Add((byte)DMReference.Type.Local);
            bytecode.AddRange(BitConverter.GetBytes(5));
            bytecode.Add((byte)Opcode.Return);

            // It should NOT try to optimize the Local reference inside PushNRefs
            byte[] optimized = BytecodeOptimizer.Optimize(bytecode.ToArray());

            Assert.That(optimized.Length, Is.EqualTo(11));
            Assert.That(optimized[0], Is.EqualTo((byte)Opcode.PushNRefs));
            Assert.That(optimized[5], Is.EqualTo((byte)DMReference.Type.Local));
            Assert.That(BitConverter.ToInt32(optimized, 6), Is.EqualTo(5));
        }
    }
}
