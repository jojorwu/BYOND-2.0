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
        public void BytecodeOptimizer_OptimizesLocalPushDereferenceCall()
        {
            // Pattern: PushLocal(0), DereferenceCall(nameId=10, argType=1, argDelta=2)
            byte[] bytecode = new byte[16];
            bytecode[0] = (byte)Opcode.PushReferenceValue;
            bytecode[1] = (byte)DMReference.Type.Local;
            BitConverter.TryWriteBytes(bytecode.AsSpan(2), 0);
            bytecode[6] = (byte)Opcode.DereferenceCall;
            BitConverter.TryWriteBytes(bytecode.AsSpan(7), 10);
            bytecode[11] = 1;
            BitConverter.TryWriteBytes(bytecode.AsSpan(12), 2);

            byte[] optimized = BytecodeOptimizer.Optimize(bytecode);

            Assert.That(optimized[0], Is.EqualTo((byte)Opcode.LocalPushDereferenceCall));
            Assert.That(BitConverter.ToInt32(optimized, 1), Is.EqualTo(0));
            Assert.That(BitConverter.ToInt32(optimized, 5), Is.EqualTo(10));
            Assert.That(optimized[9], Is.EqualTo(1));
            Assert.That(BitConverter.ToInt32(optimized, 10), Is.EqualTo(2));
        }

        [Test]
        public void BytecodeOptimizer_OptimizesLocalJumpIfTrue()
        {
            // Pattern: PushLocal(0), BooleanNot, JumpIfFalse(target)
            byte[] bytecode = new byte[13];
            bytecode[0] = (byte)Opcode.PushReferenceValue;
            bytecode[1] = (byte)DMReference.Type.Local;
            BitConverter.TryWriteBytes(bytecode.AsSpan(2), 0);
            bytecode[6] = (byte)Opcode.BooleanNot;
            bytecode[7] = (byte)Opcode.JumpIfFalse;
            BitConverter.TryWriteBytes(bytecode.AsSpan(8), 12);
            bytecode[12] = (byte)Opcode.Return;

            byte[] optimized = BytecodeOptimizer.Optimize(bytecode);

            // 1+4+4 = 9 bytes
            Assert.That(optimized[0], Is.EqualTo((byte)Opcode.LocalJumpIfTrue));
            Assert.That(BitConverter.ToInt32(optimized, 1), Is.EqualTo(0));

            // Original target 12 (Return) should be at 9 in optimized
            Assert.That(BitConverter.ToInt32(optimized, 5), Is.EqualTo(9));
        }

        [Test]
        public void BytecodeOptimizer_OptimizesLocalAddFloat()
        {
            // Pattern: PushLocal(0), PushFloat(5.0), Add
            byte[] bytecode = new byte[16];
            bytecode[0] = (byte)Opcode.PushReferenceValue;
            bytecode[1] = (byte)DMReference.Type.Local;
            BitConverter.TryWriteBytes(bytecode.AsSpan(2), 0);
            bytecode[6] = (byte)Opcode.PushFloat;
            BitConverter.TryWriteBytes(bytecode.AsSpan(7), 5.0);
            bytecode[15] = (byte)Opcode.Add;

            byte[] optimized = BytecodeOptimizer.Optimize(bytecode);

            Assert.That(optimized[0], Is.EqualTo((byte)Opcode.LocalAddFloat));
            Assert.That(BitConverter.ToInt32(optimized, 1), Is.EqualTo(0));
            Assert.That(BitConverter.ToDouble(optimized, 5), Is.EqualTo(5.0));
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

        [Test]
        public void BytecodeOptimizer_OptimizesReturnBooleans()
        {
            // Pattern: PushFloat(1.0), Return
            byte[] bytecode = new byte[10];
            bytecode[0] = (byte)Opcode.PushFloat;
            BitConverter.TryWriteBytes(bytecode.AsSpan(1), 1.0);
            bytecode[9] = (byte)Opcode.Return;

            byte[] optimized = BytecodeOptimizer.Optimize(bytecode);
            Assert.That(optimized[0], Is.EqualTo((byte)Opcode.ReturnTrue));
            Assert.That(optimized.Length, Is.EqualTo(1));

            // Pattern: PushFloat(0.0), Return
            bytecode[0] = (byte)Opcode.PushFloat;
            BitConverter.TryWriteBytes(bytecode.AsSpan(1), 0.0);
            bytecode[9] = (byte)Opcode.Return;

            optimized = BytecodeOptimizer.Optimize(bytecode);
            Assert.That(optimized[0], Is.EqualTo((byte)Opcode.ReturnFalse));
        }

        [Test]
        public void BytecodeOptimizer_OptimizesLocalPushDereferenceField()
        {
            // Pattern: PushLocal(0), DereferenceField("test")
            byte[] bytecode = new byte[11];
            bytecode[0] = (byte)Opcode.PushReferenceValue;
            bytecode[1] = (byte)DMReference.Type.Local;
            BitConverter.TryWriteBytes(bytecode.AsSpan(2), 0);
            bytecode[6] = (byte)Opcode.DereferenceField;
            BitConverter.TryWriteBytes(bytecode.AsSpan(7), 42); // stringId

            byte[] optimized = BytecodeOptimizer.Optimize(bytecode);

            Assert.That(optimized[0], Is.EqualTo((byte)Opcode.LocalPushDereferenceField));
            Assert.That(BitConverter.ToInt32(optimized, 1), Is.EqualTo(0));
            Assert.That(BitConverter.ToInt32(optimized, 5), Is.EqualTo(42));
        }

        [Test]
        public void BytecodeOptimizer_OptimizesLocalIncrementDecrement()
        {
            // Pattern: Increment(Local, 0)
            byte[] bytecode = new byte[6];
            bytecode[0] = (byte)Opcode.Increment;
            bytecode[1] = (byte)DMReference.Type.Local;
            BitConverter.TryWriteBytes(bytecode.AsSpan(2), 0);

            byte[] optimized = BytecodeOptimizer.Optimize(bytecode);
            Assert.That(optimized[0], Is.EqualTo((byte)Opcode.LocalIncrement));
            Assert.That(BitConverter.ToInt32(optimized, 1), Is.EqualTo(0));

            // Pattern: Decrement(Local, 1)
            bytecode[0] = (byte)Opcode.Decrement;
            bytecode[1] = (byte)DMReference.Type.Local;
            BitConverter.TryWriteBytes(bytecode.AsSpan(2), 1);

            optimized = BytecodeOptimizer.Optimize(bytecode);
            Assert.That(optimized[0], Is.EqualTo((byte)Opcode.LocalDecrement));
            Assert.That(BitConverter.ToInt32(optimized, 1), Is.EqualTo(1));
        }

        [Test]
        public void BytecodeOptimizer_OptimizesLocalCompareJump()
        {
            // Pattern: LocalCompareEquals(0, 1), JumpIfFalse(14)
            // 14 is the end of this bytecode
            byte[] bytecode = new byte[15];
            bytecode[0] = (byte)Opcode.LocalCompareEquals;
            BitConverter.TryWriteBytes(bytecode.AsSpan(1), 0);
            BitConverter.TryWriteBytes(bytecode.AsSpan(5), 1);
            bytecode[9] = (byte)Opcode.JumpIfFalse;
            BitConverter.TryWriteBytes(bytecode.AsSpan(10), 14);
            bytecode[14] = (byte)Opcode.Return;

            byte[] optimized = BytecodeOptimizer.Optimize(bytecode);
            Assert.That(optimized[0], Is.EqualTo((byte)Opcode.LocalCompareEqualsJumpIfFalse));
            Assert.That(BitConverter.ToInt32(optimized, 1), Is.EqualTo(0));
            Assert.That(BitConverter.ToInt32(optimized, 5), Is.EqualTo(1));

            // The return opcode starts at 14 in original, and should be at 13 in optimized
            // (A7 (1) + 4 + 4 + 0C (1) + 4 = 14) -> (B4 (1) + 4 + 4 + 4 = 13)
            Assert.That(BitConverter.ToInt32(optimized, 9), Is.EqualTo(13));
        }

        [Test]
        public void BytecodeOptimizer_PreventsOptimizationOnJumpTarget()
        {
            // Pattern: LocalCompareEquals(0, 1), JumpIfFalse(14)
            // But we add 9 (JumpIfFalse) as a jump target
            byte[] bytecode = new byte[15];
            bytecode[0] = (byte)Opcode.LocalCompareEquals;
            BitConverter.TryWriteBytes(bytecode.AsSpan(1), 0);
            BitConverter.TryWriteBytes(bytecode.AsSpan(5), 1);
            bytecode[9] = (byte)Opcode.JumpIfFalse;
            BitConverter.TryWriteBytes(bytecode.AsSpan(10), 14);
            bytecode[14] = (byte)Opcode.Return;

            // We simulate a jump to 9 by having another jump point to it
            byte[] bytecodeWithJump = new byte[20];
            bytecodeWithJump[0] = (byte)Opcode.Jump;
            BitConverter.TryWriteBytes(bytecodeWithJump.AsSpan(1), 14); // Jump to the middle
            bytecode.CopyTo(bytecodeWithJump.AsSpan(5));
            // Original JumpIfFalse is now at 5 + 9 = 14.

            byte[] optimized = BytecodeOptimizer.Optimize(bytecodeWithJump);

            // In optimized bytecode, it should NOT have LocalCompareEqualsJumpIfFalse
            bool found = false;
            foreach (byte b in optimized) if (b == (byte)Opcode.LocalCompareEqualsJumpIfFalse) found = true;

            Assert.That(found, Is.False, "Should not optimize when target is in the middle of pattern");
        }
    }
}
