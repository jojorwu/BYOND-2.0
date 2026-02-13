using System;
using System.Collections.Generic;
using Shared;

namespace Core.VM.Utils
{
    public static class BytecodeOptimizer
    {
        [ThreadStatic]
        private static List<byte>? _optimizationBuffer;

        public static byte[] Optimize(byte[] bytecode)
        {
            if (bytecode == null || bytecode.Length == 0) return bytecode ?? Array.Empty<byte>();

            _optimizationBuffer ??= new List<byte>(1024);
            var optimized = _optimizationBuffer;
            optimized.Clear();
            if (optimized.Capacity < bytecode.Length) optimized.Capacity = bytecode.Length;

            int pc = 0;

            while (pc < bytecode.Length)
            {
                // Pattern: PushReferenceValue(Local, idx)
                if (IsPushLocal(bytecode, pc, out byte idx))
                {
                    // Peek for next instruction
                    int nextPc = pc + 3;
                    if (IsPushLocal(bytecode, nextPc, out byte idx2))
                    {
                        int nextNextPc = nextPc + 3;
                        if (nextNextPc < bytecode.Length && bytecode[nextNextPc] == (byte)Opcode.Add)
                        {
                            optimized.Add((byte)Opcode.LocalPushLocalPushAdd);
                            optimized.Add(idx);
                            optimized.Add(idx2);
                            pc = nextNextPc + 1;
                            continue;
                        }

                        if (nextNextPc < bytecode.Length && bytecode[nextNextPc] == (byte)Opcode.Multiply)
                        {
                            int thirdPc = nextNextPc + 1;
                            if (IsPushLocal(bytecode, thirdPc, out byte idx3))
                            {
                                int fourthPc = thirdPc + 3;
                                if (fourthPc < bytecode.Length && bytecode[fourthPc] == (byte)Opcode.Add)
                                {
                                    optimized.Add((byte)Opcode.LocalMulAdd);
                                    optimized.Add(idx);
                                    optimized.Add(idx2);
                                    optimized.Add(idx3);
                                    pc = fourthPc + 1;
                                    continue;
                                }
                            }
                        }
                    }

                    if (pc + 3 + 4 < bytecode.Length && bytecode[pc + 3] == (byte)Opcode.PushFloat)
                    {
                        int addPc = pc + 3 + 5;
                        if (addPc < bytecode.Length && bytecode[addPc] == (byte)Opcode.Add)
                        {
                            optimized.Add((byte)Opcode.LocalAddFloat);
                            optimized.Add(idx);
                            optimized.AddRange(bytecode.AsSpan(pc + 4, 4)); // Copy float
                            pc = addPc + 1;
                            continue;
                        }
                    }

                    // Otherwise just optimize to PushLocal
                    optimized.Add((byte)Opcode.PushLocal);
                    optimized.Add(idx);
                    pc += 3;
                    continue;
                }

                // Pattern: PushReferenceValue(Argument, idx)
                if (IsPushArgument(bytecode, pc, out byte argIdx))
                {
                    optimized.Add((byte)Opcode.PushArgument);
                    optimized.Add(argIdx);
                    pc += 3;
                    continue;
                }

                // Pattern: Assign(Local, idx)
                if (IsAssignLocal(bytecode, pc, out byte assignIdx))
                {
                    optimized.Add((byte)Opcode.AssignLocal);
                    optimized.Add(assignIdx);
                    pc += 3;
                    continue;
                }

                // Default: Copy opcode
                optimized.Add(bytecode[pc++]);
            }

            return optimized.ToArray();
        }

        private static bool IsPushLocal(byte[] bytecode, int pc, out byte index)
        {
            index = 0;
            if (pc + 2 >= bytecode.Length) return false;
            if (bytecode[pc] == (byte)Opcode.PushReferenceValue && bytecode[pc + 1] == (byte)DMReference.Type.Local)
            {
                index = bytecode[pc + 2];
                return true;
            }
            return false;
        }

        private static bool IsPushArgument(byte[] bytecode, int pc, out byte index)
        {
            index = 0;
            if (pc + 2 >= bytecode.Length) return false;
            if (bytecode[pc] == (byte)Opcode.PushReferenceValue && bytecode[pc + 1] == (byte)DMReference.Type.Argument)
            {
                index = bytecode[pc + 2];
                return true;
            }
            return false;
        }

        private static bool IsAssignLocal(byte[] bytecode, int pc, out byte index)
        {
            index = 0;
            if (pc + 2 >= bytecode.Length) return false;
            if (bytecode[pc] == (byte)Opcode.Assign && bytecode[pc + 1] == (byte)DMReference.Type.Local)
            {
                index = bytecode[pc + 2];
                return true;
            }
            return false;
        }
    }
}
