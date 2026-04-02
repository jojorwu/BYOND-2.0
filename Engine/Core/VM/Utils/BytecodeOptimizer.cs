using Shared.Enums;
using System;
using System.Collections.Generic;
using Shared;

namespace Core.VM.Utils
{
    public static class BytecodeOptimizer
    {
        private const int PushLocalSize = 6;
        private const int JumpSize = 5;
        private const int FloatSize = 9;
        private const int IntSize = 5;
        private const int OpcodeSize = 1;
        private const int LabelSize = 4;
        private const int DoubleSize = 8;

        [ThreadStatic]
        private static List<byte>? _optimizationBuffer;
        [ThreadStatic]
        private static List<int>? _labelLocations;
        [ThreadStatic]
        private static int[]? _pcMap;
        [ThreadStatic]
        private static HashSet<int>? _jumpTargets;

        public static byte[] Optimize(byte[] bytecode, IReadOnlyList<string>? strings = null)
        {
            if (bytecode == null || bytecode.Length == 0) return bytecode ?? Array.Empty<byte>();

            _jumpTargets ??= new HashSet<int>();
            _jumpTargets.Clear();
            PreScanJumpTargets(bytecode, _jumpTargets);

            // Reclaim memory if buffers are excessively large (e.g., > 1GB)
            if (_optimizationBuffer != null && _optimizationBuffer.Capacity > 1073741824) _optimizationBuffer = null;
            if (_pcMap != null && _pcMap.Length > 100000000) _pcMap = null;

            _optimizationBuffer ??= new List<byte>(bytecode.Length);
            var optimized = _optimizationBuffer;
            optimized.Clear();
            if (optimized.Capacity < bytecode.Length) optimized.Capacity = bytecode.Length;

            _labelLocations ??= new List<int>();
            var labels = _labelLocations;
            labels.Clear();

            if (_pcMap == null || _pcMap.Length < bytecode.Length + 1)
                _pcMap = new int[Math.Max(1024, bytecode.Length + 1)];

            Array.Fill(_pcMap, -1);

            int pc = 0;

            while (pc < bytecode.Length)
            {
                int currentOriginalPc = pc;

                // Pattern: Consecutive Pops
                if (bytecode[pc] == (byte)Opcode.Pop && !IsJumpTarget(pc, OpcodeSize))
                {
                    int popCount = 1;
                    int scanPc = pc + OpcodeSize;
                    while (scanPc < bytecode.Length && bytecode[scanPc] == (byte)Opcode.Pop && !IsJumpTarget(scanPc, OpcodeSize))
                    {
                        popCount++;
                        scanPc++;
                    }
                    if (popCount > 1)
                    {
                        MarkPcMap(pc, scanPc, optimized.Count);
                        optimized.Add((byte)Opcode.PopN);
                        optimized.AddRange(BitConverter.GetBytes(popCount));
                        pc = scanPc;
                        continue;
                    }
                }

                if (TryOptimizePushLocalPattern(bytecode, ref pc, optimized, strings, labels)) continue;

                // Pattern: Increment(Local, idx), Pop -> LocalIncrement(idx)
                if (pc + PushLocalSize < bytecode.Length && bytecode[pc] == (byte)Opcode.Increment && bytecode[pc + 1] == (byte)DMReference.Type.Local && !IsJumpTarget(pc, PushLocalSize))
                {
                    int idxInc = BitConverter.ToInt32(bytecode, pc + 2);
                    if (pc + PushLocalSize < bytecode.Length && bytecode[pc + PushLocalSize] == (byte)Opcode.Pop && !IsJumpTarget(pc + PushLocalSize, OpcodeSize))
                    {
                        MarkPcMap(pc, pc + PushLocalSize + OpcodeSize, optimized.Count);
                        optimized.Add((byte)Opcode.LocalIncrement);
                        optimized.AddRange(BitConverter.GetBytes(idxInc));
                        pc += PushLocalSize + OpcodeSize;
                        continue;
                    }
                }

                // Pattern: Decrement(Local, idx), Pop -> LocalDecrement(idx)
                if (pc + PushLocalSize < bytecode.Length && bytecode[pc] == (byte)Opcode.Decrement && bytecode[pc + 1] == (byte)DMReference.Type.Local && !IsJumpTarget(pc, PushLocalSize))
                {
                    int idxDec = BitConverter.ToInt32(bytecode, pc + 2);
                    if (pc + PushLocalSize < bytecode.Length && bytecode[pc + PushLocalSize] == (byte)Opcode.Pop && !IsJumpTarget(pc + PushLocalSize, OpcodeSize))
                    {
                        MarkPcMap(pc, pc + PushLocalSize + OpcodeSize, optimized.Count);
                        optimized.Add((byte)Opcode.LocalDecrement);
                        optimized.AddRange(BitConverter.GetBytes(idxDec));
                        pc += PushLocalSize + OpcodeSize;
                        continue;
                    }
                }

                if (TryOptimizeReturnPattern(bytecode, ref pc, optimized)) continue;

                // Pattern: PushReferenceValue(Argument, idx)
                if (IsPushArgument(bytecode, pc, out int argIdx))
                {
                    MarkPcMap(pc, pc + PushLocalSize, optimized.Count);
                    optimized.Add((byte)Opcode.PushArgument);
                    optimized.AddRange(BitConverter.GetBytes(argIdx));
                    pc += PushLocalSize;
                    continue;
                }

                // Pattern: Assign(Local, idx)
                if (IsAssignLocal(bytecode, pc, out int assignIdx))
                {
                    if (assignIdx >= 0 && assignIdx < 16)
                    {
                        MarkPcMap(pc, pc + PushLocalSize, optimized.Count);
                        optimized.Add((byte)((int)Opcode.AssignLocal0 + assignIdx));
                        pc += PushLocalSize;
                        continue;
                    }

                    MarkPcMap(pc, pc + PushLocalSize, optimized.Count);
                    optimized.Add((byte)Opcode.AssignLocal);
                    optimized.AddRange(BitConverter.GetBytes(assignIdx));
                    pc += PushLocalSize;
                    continue;
                }

                // Pattern: GetVariable(stringId) where stringId is a built-in
                if (strings != null && pc + 4 < bytecode.Length && bytecode[pc] == (byte)Opcode.GetVariable)
                {
                    int stringId = BitConverter.ToInt32(bytecode, pc + 1);
                    if (stringId >= 0 && stringId < strings.Count)
                    {
                        var builtin = GetBuiltinVarType(strings[stringId]);
                        if (builtin.HasValue)
                        {
                            MarkPcMap(pc, pc + 5, optimized.Count);
                            optimized.Add((byte)Opcode.GetBuiltinVar);
                            optimized.Add((byte)builtin.Value);
                            pc += 5;
                            continue;
                        }
                    }
                }

                // Pattern: SetVariable(stringId) where stringId is a built-in
                if (strings != null && pc + 4 < bytecode.Length && bytecode[pc] == (byte)Opcode.SetVariable)
                {
                    int stringId = BitConverter.ToInt32(bytecode, pc + 1);
                    if (stringId >= 0 && stringId < strings.Count)
                    {
                        var builtin = GetBuiltinVarType(strings[stringId]);
                        if (builtin.HasValue)
                        {
                            MarkPcMap(pc, pc + 5, optimized.Count);
                            optimized.Add((byte)Opcode.SetBuiltinVar);
                            optimized.Add((byte)builtin.Value);
                            pc += 5;
                            continue;
                        }
                    }
                }

                // Pattern: Call(GlobalProc, procId, ...) -> CallGlobalProc(procId, ...)
                if (pc + 6 < bytecode.Length && bytecode[pc] == (byte)Opcode.Call && bytecode[pc + 1] == (byte)DMReference.Type.GlobalProc && !IsJumpTarget(pc, 1))
                {
                    int procId = BitConverter.ToInt32(bytecode, pc + 2);
                    int callArgTypePc = pc + 6;
                    if (callArgTypePc + 8 < bytecode.Length)
                    {
                        MarkPcMap(pc, callArgTypePc + 9, optimized.Count);
                        optimized.Add((byte)Opcode.CallGlobalProc);
                        optimized.AddRange(BitConverter.GetBytes(procId));
                        optimized.Add(bytecode[callArgTypePc]); // argType
                        optimized.AddRange(bytecode.AsSpan(callArgTypePc + 1, 4)); // argStackDelta
                        pc = callArgTypePc + 9;
                        continue;
                    }
                }

                // Pattern: PushReferenceValue(ref), JumpIfFalse(label) -> JumpIfReferenceFalse(ref, label)
                if (pc + OpcodeSize < bytecode.Length && bytecode[pc] == (byte)Opcode.PushReferenceValue && !IsJumpTarget(pc, OpcodeSize + GetReferenceSize(bytecode, pc + OpcodeSize)))
                {
                    int refSize = GetReferenceSize(bytecode, pc + OpcodeSize);
                    int jumpPc = pc + OpcodeSize + refSize;
                    if (jumpPc + LabelSize < bytecode.Length && bytecode[jumpPc] == (byte)Opcode.JumpIfFalse && !IsJumpTarget(jumpPc, JumpSize))
                    {
                        // Fusion: PushReferenceValue(Global, idx), JumpIfFalse(label) -> GlobalJumpIfFalse(idx, label)
                        if (bytecode[pc + OpcodeSize] == (byte)DMReference.Type.Global)
                        {
                            MarkPcMap(pc, jumpPc + JumpSize, optimized.Count);
                            optimized.Add((byte)Opcode.GlobalJumpIfFalse);
                            optimized.AddRange(bytecode.AsSpan(pc + 2, LabelSize)); // Global index
                            labels.Add(optimized.Count);
                            optimized.AddRange(bytecode.AsSpan(jumpPc + OpcodeSize, LabelSize));
                            pc = jumpPc + JumpSize;
                            continue;
                        }

                        MarkPcMap(pc, jumpPc + JumpSize, optimized.Count);
                        optimized.Add((byte)Opcode.JumpIfReferenceFalse);
                        optimized.AddRange(bytecode.AsSpan(pc + OpcodeSize, refSize));
                        labels.Add(optimized.Count);
                        optimized.AddRange(bytecode.AsSpan(jumpPc + OpcodeSize, LabelSize));
                        pc = jumpPc + JumpSize;
                        continue;
                    }
                }

                // Default: Copy opcode and arguments, tracking labels
                _pcMap[pc] = optimized.Count;
                Opcode opcode = (Opcode)bytecode[pc++];
                optimized.Add((byte)opcode);

                var metadata = OpcodeMetadataCache.GetMetadata(opcode);
                if (metadata.RequiredArgs == null) continue;

                int varCount = 0;
                foreach (var argType in metadata.RequiredArgs)
                {
                    if (argType == OpcodeArgType.Label)
                    {
                        labels.Add(optimized.Count);
                        optimized.AddRange(bytecode.AsSpan(pc, 4));
                        pc += 4;
                    }
                    else
                    {
                        int size = GetArgSize(argType, bytecode, pc);
                        if (size == 4 && metadata.VariableArgs) varCount = BitConverter.ToInt32(bytecode, pc);
                        optimized.AddRange(bytecode.AsSpan(pc, size));
                        pc += size;
                    }
                }

                if (metadata.VariableArgs)
                {
                    var argType = GetVariableArgType(opcode);
                    for (int i = 0; i < varCount; i++)
                    {
                        int size = GetArgSize(argType, bytecode, pc);
                        optimized.AddRange(bytecode.AsSpan(pc, size));
                        pc += size;
                    }
                }
            }

            _pcMap[pc] = optimized.Count; // End of bytecode

            // Pass 2: Fixup labels
            byte[] result = optimized.ToArray();
            foreach (int labelLoc in labels)
            {
                int originalTarget = BitConverter.ToInt32(result, labelLoc);
                if (originalTarget >= 0 && originalTarget < _pcMap.Length)
                {
                    int optimizedTarget = _pcMap[originalTarget];
                    if (optimizedTarget == -1)
                    {
                        // Target was inside an optimized block, find nearest
                        for (int i = originalTarget; i < _pcMap.Length; i++)
                        {
                            if (_pcMap[i] != -1)
                            {
                                optimizedTarget = _pcMap[i];
                                break;
                            }
                        }
                    }

                    var targetBytes = BitConverter.GetBytes(optimizedTarget);
                    for (int i = 0; i < 4; i++) result[labelLoc + i] = targetBytes[i];
                }
            }

            return result;
        }

        private static bool IsJumpTarget(int start, int count)
        {
            if (_jumpTargets == null) return false;
            for (int i = 1; i < count; i++) // i=1 because jumping to the START of a pattern is okay
            {
                if (_jumpTargets.Contains(start + i)) return true;
            }
            return false;
        }

        private static bool TryOptimizePushLocalPattern(byte[] bytecode, ref int pc, List<byte> optimized, IReadOnlyList<string>? strings, List<int> labels)
        {
            if (IsPushLocal(bytecode, pc, out int idx) && !IsJumpTarget(pc, 6))
            {
                int nextPc = pc + 6;
                if (IsPushLocal(bytecode, nextPc, out int idx2) && !IsJumpTarget(nextPc, 6))
                {
                    int nextNextPc = nextPc + 6;

                    if (nextNextPc < bytecode.Length && !IsJumpTarget(nextNextPc, 1))
                    {
                        if (bytecode[nextNextPc] == (byte)Opcode.CompareEquals)
                        {
                            int jumpPc = nextNextPc + 1;
                            if (jumpPc + 4 < bytecode.Length && bytecode[jumpPc] == (byte)Opcode.JumpIfFalse && !IsJumpTarget(jumpPc, 5))
                            {
                                MarkPcMap(pc, jumpPc + 5, optimized.Count);
                                optimized.Add((byte)Opcode.LocalCompareEqualsJumpIfFalse);
                                optimized.AddRange(BitConverter.GetBytes(idx));
                                optimized.AddRange(BitConverter.GetBytes(idx2));
                                labels.Add(optimized.Count);
                                optimized.AddRange(bytecode.AsSpan(jumpPc + 1, 4));
                                pc = jumpPc + 5;
                                return true;
                            }

                            MarkPcMap(pc, nextNextPc + 1, optimized.Count);
                            optimized.Add((byte)Opcode.LocalCompareEquals);
                            optimized.AddRange(BitConverter.GetBytes(idx));
                            optimized.AddRange(BitConverter.GetBytes(idx2));
                            pc = nextNextPc + 1;
                            return true;
                        }

                        if (bytecode[nextNextPc] == (byte)Opcode.CompareLessThan)
                        {
                            MarkPcMap(pc, nextNextPc + 1, optimized.Count);
                            optimized.Add((byte)Opcode.LocalCompareLessThan);
                            optimized.AddRange(BitConverter.GetBytes(idx));
                            optimized.AddRange(BitConverter.GetBytes(idx2));
                            pc = nextNextPc + 1;
                            return true;
                        }

                        if (bytecode[nextNextPc] == (byte)Opcode.CompareGreaterThan)
                        {
                            MarkPcMap(pc, nextNextPc + 1, optimized.Count);
                            optimized.Add((byte)Opcode.LocalCompareGreaterThan);
                            optimized.AddRange(BitConverter.GetBytes(idx));
                            optimized.AddRange(BitConverter.GetBytes(idx2));
                            pc = nextNextPc + 1;
                            return true;
                        }

                        if (bytecode[nextNextPc] == (byte)Opcode.CompareLessThanOrEqual)
                        {
                            MarkPcMap(pc, nextNextPc + 1, optimized.Count);
                            optimized.Add((byte)Opcode.LocalCompareLessThanOrEqual);
                            optimized.AddRange(BitConverter.GetBytes(idx));
                            optimized.AddRange(BitConverter.GetBytes(idx2));
                            pc = nextNextPc + 1;
                            return true;
                        }

                        if (bytecode[nextNextPc] == (byte)Opcode.CompareGreaterThanOrEqual)
                        {
                            MarkPcMap(pc, nextNextPc + 1, optimized.Count);
                            optimized.Add((byte)Opcode.LocalCompareGreaterThanOrEqual);
                            optimized.AddRange(BitConverter.GetBytes(idx));
                            optimized.AddRange(BitConverter.GetBytes(idx2));
                            pc = nextNextPc + 1;
                            return true;
                        }

                        if (bytecode[nextNextPc] == (byte)Opcode.CompareNotEquals)
                        {
                            int jumpPc = nextNextPc + 1;
                            if (jumpPc + 4 < bytecode.Length && bytecode[jumpPc] == (byte)Opcode.JumpIfFalse && !IsJumpTarget(jumpPc, 5))
                            {
                                MarkPcMap(pc, jumpPc + 5, optimized.Count);
                                optimized.Add((byte)Opcode.LocalCompareNotEqualsJumpIfFalse);
                                optimized.AddRange(BitConverter.GetBytes(idx));
                                optimized.AddRange(BitConverter.GetBytes(idx2));
                                labels.Add(optimized.Count);
                                optimized.AddRange(bytecode.AsSpan(jumpPc + 1, 4));
                                pc = jumpPc + 5;
                                return true;
                            }

                            MarkPcMap(pc, nextNextPc + 1, optimized.Count);
                            optimized.Add((byte)Opcode.LocalCompareNotEquals);
                            optimized.AddRange(BitConverter.GetBytes(idx));
                            optimized.AddRange(BitConverter.GetBytes(idx2));
                            pc = nextNextPc + 1;
                            return true;
                        }

                        if (bytecode[nextNextPc] == (byte)Opcode.Add)
                        {
                            // Optimization: if both locals are in specialized range, PushLocalN + PushLocalN + Add is 3 bytes,
                            // while LocalPushLocalPushAdd is 9 bytes.
                            if (idx >= 16 || idx2 >= 16)
                            {
                                if (TryOptimizeArithmeticPattern(bytecode, pc, nextNextPc, idx, idx2, Opcode.LocalAddLocalAssign, Opcode.LocalPushLocalPushAdd, optimized, out int newPc))
                                {
                                    pc = newPc;
                                    return true;
                                }
                            }
                        }

                        if (bytecode[nextNextPc] == (byte)Opcode.Subtract)
                        {
                            if (idx >= 16 || idx2 >= 16)
                            {
                                if (TryOptimizeArithmeticPattern(bytecode, pc, nextNextPc, idx, idx2, Opcode.LocalSubLocalAssign, Opcode.LocalPushLocalPushSub, optimized, out int newPc))
                                {
                                    pc = newPc;
                                    return true;
                                }
                            }
                        }

                        if (bytecode[nextNextPc] == (byte)Opcode.Multiply)
                        {
                            if (idx >= 16 || idx2 >= 16)
                            {
                                if (TryOptimizeArithmeticPattern(bytecode, pc, nextNextPc, idx, idx2, Opcode.LocalMulLocalAssign, Opcode.LocalPushLocalPushMul, optimized, out int newPc))
                                {
                                    pc = newPc;
                                    return true;
                                }
                            }

                            int thirdPc = nextNextPc + 1;
                            if (IsPushLocal(bytecode, thirdPc, out int idx3) && !IsJumpTarget(thirdPc, 6))
                            {
                                int fourthPc = thirdPc + 6;
                                if (fourthPc < bytecode.Length && bytecode[fourthPc] == (byte)Opcode.Add && !IsJumpTarget(fourthPc, 1))
                                {
                                    MarkPcMap(pc, fourthPc + 1, optimized.Count);
                                    optimized.Add((byte)Opcode.LocalMulAdd);
                                    optimized.AddRange(BitConverter.GetBytes(idx));
                                    optimized.AddRange(BitConverter.GetBytes(idx2));
                                    optimized.AddRange(BitConverter.GetBytes(idx3));
                                    pc = fourthPc + 1;
                                    return true;
                                }
                            }
                        }

                        if (bytecode[nextNextPc] == (byte)Opcode.Divide)
                        {
                            if (idx >= 16 || idx2 >= 16)
                            {
                                if (TryOptimizeArithmeticPattern(bytecode, pc, nextNextPc, idx, idx2, Opcode.LocalDivLocalAssign, Opcode.LocalPushLocalPushDiv, optimized, out int newPc))
                                {
                                    pc = newPc;
                                    return true;
                                }
                            }
                        }

                        if (bytecode[nextNextPc] == (byte)Opcode.CompareLessThan)
                        {
                            int jumpPc = nextNextPc + 1;
                            if (jumpPc + 4 < bytecode.Length && bytecode[jumpPc] == (byte)Opcode.JumpIfFalse && !IsJumpTarget(jumpPc, 5))
                            {
                                MarkPcMap(pc, jumpPc + 5, optimized.Count);
                                optimized.Add((byte)Opcode.LocalCompareLessThanJumpIfFalse);
                                optimized.AddRange(BitConverter.GetBytes(idx));
                                optimized.AddRange(BitConverter.GetBytes(idx2));
                                labels.Add(optimized.Count);
                                optimized.AddRange(bytecode.AsSpan(jumpPc + 1, 4));
                                pc = jumpPc + 5;
                                return true;
                            }
                        }

                        if (bytecode[nextNextPc] == (byte)Opcode.CompareGreaterThan)
                        {
                            int jumpPc = nextNextPc + 1;
                            if (jumpPc + 4 < bytecode.Length && bytecode[jumpPc] == (byte)Opcode.JumpIfFalse && !IsJumpTarget(jumpPc, 5))
                            {
                                MarkPcMap(pc, jumpPc + 5, optimized.Count);
                                optimized.Add((byte)Opcode.LocalCompareGreaterThanJumpIfFalse);
                                optimized.AddRange(BitConverter.GetBytes(idx));
                                optimized.AddRange(BitConverter.GetBytes(idx2));
                                labels.Add(optimized.Count);
                                optimized.AddRange(bytecode.AsSpan(jumpPc + 1, 4));
                                pc = jumpPc + 5;
                                return true;
                            }
                        }

                        if (bytecode[nextNextPc] == (byte)Opcode.CompareLessThanOrEqual)
                        {
                            int jumpPc = nextNextPc + 1;
                            if (jumpPc + 4 < bytecode.Length && bytecode[jumpPc] == (byte)Opcode.JumpIfFalse && !IsJumpTarget(jumpPc, 5))
                            {
                                MarkPcMap(pc, jumpPc + 5, optimized.Count);
                                optimized.Add((byte)Opcode.LocalCompareLessThanOrEqualJumpIfFalse);
                                optimized.AddRange(BitConverter.GetBytes(idx));
                                optimized.AddRange(BitConverter.GetBytes(idx2));
                                labels.Add(optimized.Count);
                                optimized.AddRange(bytecode.AsSpan(jumpPc + 1, 4));
                                pc = jumpPc + 5;
                                return true;
                            }
                        }

                        if (bytecode[nextNextPc] == (byte)Opcode.CompareGreaterThanOrEqual)
                        {
                            int jumpPc = nextNextPc + 1;
                            if (jumpPc + 4 < bytecode.Length && bytecode[jumpPc] == (byte)Opcode.JumpIfFalse && !IsJumpTarget(jumpPc, 5))
                            {
                                MarkPcMap(pc, jumpPc + 5, optimized.Count);
                                optimized.Add((byte)Opcode.LocalCompareGreaterThanOrEqualJumpIfFalse);
                                optimized.AddRange(BitConverter.GetBytes(idx));
                                optimized.AddRange(BitConverter.GetBytes(idx2));
                                labels.Add(optimized.Count);
                                optimized.AddRange(bytecode.AsSpan(jumpPc + 1, 4));
                                pc = jumpPc + 5;
                                return true;
                            }
                        }

                        int comparisonPc = nextPc + 9;
                        if (bytecode[comparisonPc] == (byte)Opcode.CompareLessThan)
                        {
                            int jumpPc = comparisonPc + 1;
                            if (jumpPc + 4 < bytecode.Length && bytecode[jumpPc] == (byte)Opcode.JumpIfFalse && !IsJumpTarget(jumpPc, 5))
                            {
                                MarkPcMap(pc, jumpPc + 5, optimized.Count);
                                optimized.Add((byte)Opcode.LocalCompareLessThanFloatJumpIfFalse);
                                optimized.AddRange(BitConverter.GetBytes(idx));
                                optimized.AddRange(bytecode.AsSpan(nextPc + 1, 8));
                                labels.Add(optimized.Count);
                                optimized.AddRange(bytecode.AsSpan(jumpPc + 1, 4));
                                pc = jumpPc + 5;
                                return true;
                            }
                        }

                        if (bytecode[comparisonPc] == (byte)Opcode.CompareGreaterThan)
                        {
                            int jumpPc = comparisonPc + 1;
                            if (jumpPc + 4 < bytecode.Length && bytecode[jumpPc] == (byte)Opcode.JumpIfFalse && !IsJumpTarget(jumpPc, 5))
                            {
                                MarkPcMap(pc, jumpPc + 5, optimized.Count);
                                optimized.Add((byte)Opcode.LocalCompareGreaterThanFloatJumpIfFalse);
                                optimized.AddRange(BitConverter.GetBytes(idx));
                                optimized.AddRange(bytecode.AsSpan(nextPc + 1, 8));
                                labels.Add(optimized.Count);
                                optimized.AddRange(bytecode.AsSpan(jumpPc + 1, 4));
                                pc = jumpPc + 5;
                                return true;
                            }
                        }

                        if (bytecode[comparisonPc] == (byte)Opcode.CompareLessThanOrEqual)
                        {
                            int jumpPc = comparisonPc + 1;
                            if (jumpPc + 4 < bytecode.Length && bytecode[jumpPc] == (byte)Opcode.JumpIfFalse && !IsJumpTarget(jumpPc, 5))
                            {
                                MarkPcMap(pc, jumpPc + 5, optimized.Count);
                                optimized.Add((byte)Opcode.LocalCompareLessThanOrEqualFloatJumpIfFalse);
                                optimized.AddRange(BitConverter.GetBytes(idx));
                                optimized.AddRange(bytecode.AsSpan(nextPc + 1, 8));
                                labels.Add(optimized.Count);
                                optimized.AddRange(bytecode.AsSpan(jumpPc + 1, 4));
                                pc = jumpPc + 5;
                                return true;
                            }
                        }

                        if (bytecode[comparisonPc] == (byte)Opcode.CompareGreaterThanOrEqual)
                        {
                            int jumpPc = comparisonPc + 1;
                            if (jumpPc + 4 < bytecode.Length && bytecode[jumpPc] == (byte)Opcode.JumpIfFalse && !IsJumpTarget(jumpPc, 5))
                            {
                                MarkPcMap(pc, jumpPc + 5, optimized.Count);
                                optimized.Add((byte)Opcode.LocalCompareGreaterThanOrEqualFloatJumpIfFalse);
                                optimized.AddRange(BitConverter.GetBytes(idx));
                                optimized.AddRange(bytecode.AsSpan(nextPc + 1, 8));
                                labels.Add(optimized.Count);
                                optimized.AddRange(bytecode.AsSpan(jumpPc + 1, 4));
                                pc = jumpPc + 5;
                                return true;
                            }
                        }
                    }
                }

                if (nextPc + 9 < bytecode.Length && bytecode[nextPc] == (byte)Opcode.PushFloat && !IsJumpTarget(nextPc, 9))
                {
                    int opPc = nextPc + 9;
                    if (opPc < bytecode.Length && !IsJumpTarget(opPc, 1))
                    {
                        if (bytecode[opPc] == (byte)Opcode.Add)
                        {
                            if (TryOptimizeArithmeticFloatPattern(bytecode, pc, nextPc, opPc, idx, Opcode.LocalAddFloatAssign, Opcode.LocalAddFloat, optimized, out int newPc))
                            {
                                pc = newPc;
                                return true;
                            }
                        }
                        if (bytecode[opPc] == (byte)Opcode.Multiply)
                        {
                            if (TryOptimizeArithmeticFloatPattern(bytecode, pc, nextPc, opPc, idx, Opcode.LocalMulFloatAssign, Opcode.LocalMulFloat, optimized, out int newPc))
                            {
                                pc = newPc;
                                return true;
                            }
                        }
                        if (bytecode[opPc] == (byte)Opcode.Divide)
                        {
                            if (TryOptimizeArithmeticFloatPattern(bytecode, pc, nextPc, opPc, idx, Opcode.LocalDivFloatAssign, Opcode.LocalDivFloat, optimized, out int newPc))
                            {
                                pc = newPc;
                                return true;
                            }
                        }
                    }
                }

                if (nextPc + 4 < bytecode.Length && bytecode[nextPc] == (byte)Opcode.DereferenceField && !IsJumpTarget(nextPc, 5))
                {
                    int derefPc = nextPc;
                    int nameId = BitConverter.ToInt32(bytecode, derefPc + 1);

                    int assignPc = derefPc + 5;
                    if (assignPc + 5 < bytecode.Length && !IsJumpTarget(assignPc, 6))
                    {
                        if (IsAssignLocal(bytecode, assignPc, out int targetIdx))
                        {
                            int popPc = assignPc + 6;
                            if (popPc < bytecode.Length && bytecode[popPc] == (byte)Opcode.Pop && !IsJumpTarget(popPc, 1))
                            {
                                MarkPcMap(pc, popPc + 1, optimized.Count);
                                optimized.Add((byte)Opcode.LocalFieldTransfer);
                                optimized.AddRange(BitConverter.GetBytes(idx)); // Source local
                                optimized.AddRange(BitConverter.GetBytes(nameId)); // Field name
                                optimized.AddRange(BitConverter.GetBytes(targetIdx)); // Target local
                                pc = popPc + 1;
                                return true;
                            }
                        }
                    }

                    int branchPc = derefPc + 5;
                    if (branchPc + 4 < bytecode.Length && !IsJumpTarget(branchPc, 5))
                    {
                        if (bytecode[branchPc] == (byte)Opcode.JumpIfFalse)
                        {
                            MarkPcMap(pc, branchPc + 5, optimized.Count);
                            optimized.Add((byte)Opcode.LocalJumpIfFieldFalse);
                            optimized.AddRange(BitConverter.GetBytes(idx));
                            optimized.AddRange(BitConverter.GetBytes(nameId));
                            labels.Add(optimized.Count);
                            optimized.AddRange(bytecode.AsSpan(branchPc + 1, 4));
                            pc = branchPc + 5;
                            return true;
                        }
                        if (bytecode[branchPc] == (byte)Opcode.BooleanNot)
                        {
                            int jumpPc = branchPc + 1;
                            if (jumpPc + 4 < bytecode.Length && bytecode[jumpPc] == (byte)Opcode.JumpIfFalse && !IsJumpTarget(jumpPc, 5))
                            {
                                MarkPcMap(pc, jumpPc + 5, optimized.Count);
                                optimized.Add((byte)Opcode.LocalJumpIfFieldTrue);
                                optimized.AddRange(BitConverter.GetBytes(idx));
                                optimized.AddRange(BitConverter.GetBytes(nameId));
                                labels.Add(optimized.Count);
                                optimized.AddRange(bytecode.AsSpan(jumpPc + 1, 4));
                                pc = jumpPc + 5;
                                return true;
                            }
                        }
                    }

                    int callPc = derefPc + 5;
                    if (callPc + 8 < bytecode.Length && bytecode[callPc] == (byte)Opcode.DereferenceCall && !IsJumpTarget(callPc, 10))
                    {
                        MarkPcMap(pc, callPc + 10, optimized.Count);
                        optimized.Add((byte)Opcode.LocalPushDereferenceCall);
                        optimized.AddRange(BitConverter.GetBytes(idx));
                        optimized.AddRange(BitConverter.GetBytes(nameId));
                        optimized.AddRange(bytecode.AsSpan(callPc + 5, 5)); // argType, argStackDelta
                        pc = callPc + 10;
                        return true;
                    }

                    MarkPcMap(pc, nextPc + 5, optimized.Count);
                    optimized.Add((byte)Opcode.LocalPushDereferenceField);
                    optimized.AddRange(BitConverter.GetBytes(idx));
                    optimized.AddRange(bytecode.AsSpan(nextPc + 1, 4)); // nameId
                    pc = nextPc + 5;
                    return true;
                }

                if (nextPc < bytecode.Length && bytecode[nextPc] == (byte)Opcode.DereferenceIndex && !IsJumpTarget(nextPc, 1))
                {
                    MarkPcMap(pc, nextPc + 1, optimized.Count);
                    optimized.Add((byte)Opcode.LocalPushDereferenceIndex);
                    optimized.AddRange(BitConverter.GetBytes(idx));
                    pc = nextPc + 1;
                    return true;
                }

                if (nextPc + 4 < bytecode.Length && !IsJumpTarget(nextPc, 1))
                {
                    if (bytecode[nextPc] == (byte)Opcode.JumpIfNull || (bytecode[nextPc] == (byte)Opcode.JumpIfNullNoPop))
                    {
                        MarkPcMap(pc, nextPc + 5, optimized.Count);
                        optimized.Add((byte)Opcode.LocalJumpIfNull);
                        optimized.AddRange(BitConverter.GetBytes(idx));
                        labels.Add(optimized.Count);
                        optimized.AddRange(bytecode.AsSpan(nextPc + 1, 4));
                        pc = nextPc + 5;
                        return true;
                    }

                    if (bytecode[nextPc] == (byte)Opcode.JumpIfFalse)
                    {
                        MarkPcMap(pc, nextPc + 5, optimized.Count);
                        optimized.Add((byte)Opcode.LocalJumpIfFalse);
                        optimized.AddRange(BitConverter.GetBytes(idx));
                        labels.Add(optimized.Count);
                        optimized.AddRange(bytecode.AsSpan(nextPc + 1, 4));
                        pc = nextPc + 5;
                        return true;
                    }

                    if (nextPc + 1 < bytecode.Length && bytecode[nextPc] == (byte)Opcode.BooleanNot && !IsJumpTarget(nextPc, 1))
                    {
                        int jumpPc = nextPc + 1;
                        if (jumpPc + 4 < bytecode.Length && bytecode[jumpPc] == (byte)Opcode.JumpIfFalse && !IsJumpTarget(jumpPc, 5))
                        {
                            MarkPcMap(pc, jumpPc + 5, optimized.Count);
                            optimized.Add((byte)Opcode.LocalJumpIfTrue);
                            optimized.AddRange(BitConverter.GetBytes(idx));
                            labels.Add(optimized.Count);
                            optimized.AddRange(bytecode.AsSpan(jumpPc + 1, 4));
                            pc = jumpPc + 5;
                            return true;
                        }
                    }

                    if (bytecode[nextPc] == (byte)Opcode.Return)
                    {
                        MarkPcMap(pc, nextPc + 1, optimized.Count);
                        optimized.Add((byte)Opcode.LocalPushReturn);
                        optimized.AddRange(BitConverter.GetBytes(idx));
                        pc = nextPc + 1;
                        return true;
                    }
                }

                // Otherwise just optimize to PushLocal
                if (idx >= 0 && idx < 16)
                {
                    MarkPcMap(pc, pc + 6, optimized.Count);
                    optimized.Add((byte)((int)Opcode.PushLocal0 + idx));
                    pc += 6;
                    return true;
                }

                MarkPcMap(pc, pc + 6, optimized.Count);
                optimized.Add((byte)Opcode.PushLocal);
                optimized.AddRange(BitConverter.GetBytes(idx));
                pc += 6;
                return true;
            }
            return false;
        }

        private static bool TryOptimizeReturnPattern(byte[] bytecode, ref int pc, List<byte> optimized)
        {
            // Pattern: PushNull, Return
            if (pc + 1 < bytecode.Length && bytecode[pc] == (byte)Opcode.PushNull && bytecode[pc + 1] == (byte)Opcode.Return && !IsJumpTarget(pc + 1, 1))
            {
                MarkPcMap(pc, pc + 2, optimized.Count);
                optimized.Add((byte)Opcode.ReturnNull);
                pc += 2;
                return true;
            }

            // Pattern: PushFloat, Return
            if (pc + 9 < bytecode.Length && bytecode[pc] == (byte)Opcode.PushFloat && bytecode[pc + 9] == (byte)Opcode.Return && !IsJumpTarget(pc + 9, 1))
            {
                double val = BitConverter.ToDouble(bytecode, pc + 1);
                if (val == 1.0)
                {
                    MarkPcMap(pc, pc + 10, optimized.Count);
                    optimized.Add((byte)Opcode.ReturnTrue);
                    pc += 10;
                    return true;
                }
                else if (val == 0.0)
                {
                    MarkPcMap(pc, pc + 10, optimized.Count);
                    optimized.Add((byte)Opcode.ReturnFalse);
                    pc += 10;
                    return true;
                }
            }
            return false;
        }

        private static bool TryOptimizeArithmeticFloatPattern(byte[] bytecode, int pc, int floatPc, int opPc, int idx, Opcode assignOp, Opcode pushOp, List<byte> optimized, out int newPc)
        {
            int assignPc = opPc + 1;
            if (IsAssignLocal(bytecode, assignPc, out int targetIdx) && targetIdx == idx && !IsJumpTarget(assignPc, 6))
            {
                int popPc = assignPc + 6;
                if (popPc < bytecode.Length && bytecode[popPc] == (byte)Opcode.Pop && !IsJumpTarget(popPc, 1))
                {
                    MarkPcMap(pc, popPc + 1, optimized.Count);
                    optimized.Add((byte)assignOp);
                    optimized.AddRange(BitConverter.GetBytes(idx));
                    optimized.AddRange(bytecode.AsSpan(floatPc + 1, 8));
                    newPc = popPc + 1;
                    return true;
                }
            }

            MarkPcMap(pc, opPc + 1, optimized.Count);
            optimized.Add((byte)pushOp);
            optimized.AddRange(BitConverter.GetBytes(idx));
            optimized.AddRange(bytecode.AsSpan(floatPc + 1, 8));
            newPc = opPc + 1;
            return true;
        }

        private static bool TryOptimizeArithmeticPattern(byte[] bytecode, int pc, int opPc, int idx1, int idx2, Opcode assignOp, Opcode pushOp, List<byte> optimized, out int newPc)
        {
            int assignPc = opPc + 1;
            if (IsAssignLocal(bytecode, assignPc, out int targetIdx) && targetIdx == idx1 && !IsJumpTarget(assignPc, 6))
            {
                int popPc = assignPc + 6;
                if (popPc < bytecode.Length && bytecode[popPc] == (byte)Opcode.Pop && !IsJumpTarget(popPc, 1))
                {
                    MarkPcMap(pc, popPc + 1, optimized.Count);
                    optimized.Add((byte)assignOp);
                    optimized.AddRange(BitConverter.GetBytes(idx1));
                    optimized.AddRange(BitConverter.GetBytes(idx2));
                    newPc = popPc + 1;
                    return true;
                }
            }

            MarkPcMap(pc, opPc + 1, optimized.Count);
            optimized.Add((byte)pushOp);
            optimized.AddRange(BitConverter.GetBytes(idx1));
            optimized.AddRange(BitConverter.GetBytes(idx2));
            newPc = opPc + 1;
            return true;
        }

        private static void MarkPcMap(int start, int end, int optimizedPc)
        {
            for (int i = start; i < end; i++) _pcMap![i] = optimizedPc;
        }

        private static int GetArgSize(OpcodeArgType type, byte[] bytecode, int pc)
        {
            return type switch
            {
                OpcodeArgType.ArgType => 1,
                OpcodeArgType.StackDelta => 4,
                OpcodeArgType.Resource => 4,
                OpcodeArgType.TypeId => 4,
                OpcodeArgType.ProcId => 4,
                OpcodeArgType.EnumeratorId => 1,
                OpcodeArgType.FilterId => 4,
                OpcodeArgType.ListSize => 4,
                OpcodeArgType.Int => 4,
                OpcodeArgType.Label => 4,
                OpcodeArgType.Float => 8,
                OpcodeArgType.String => 4,
                OpcodeArgType.Reference => GetReferenceSize(bytecode, pc),
                OpcodeArgType.FormatCount => 4,
                OpcodeArgType.PickCount => 4,
                OpcodeArgType.ConcatCount => 4,
                _ => 0
            };
        }

        private static int GetReferenceSize(byte[] bytecode, int pc)
        {
            if (pc >= bytecode.Length) return 0;
            var type = (DMReference.Type)bytecode[pc];
            return type switch
            {
                DMReference.Type.Src => 1,
                DMReference.Type.Self => 1,
                DMReference.Type.Usr => 1,
                DMReference.Type.World => 1,
                DMReference.Type.Args => 1,
                DMReference.Type.Global => 5,
                DMReference.Type.Local => 5,
                DMReference.Type.Argument => 5,
                DMReference.Type.Field => 5,
                DMReference.Type.ListIndex => 1,
                _ => 1
            };
        }

        private static OpcodeArgType GetVariableArgType(Opcode opcode)
        {
            return opcode switch
            {
                Opcode.PushNRefs => OpcodeArgType.Reference,
                Opcode.PushNFloats => OpcodeArgType.Float,
                Opcode.PushNResources => OpcodeArgType.Resource,
                Opcode.PushNStrings => OpcodeArgType.String,
                Opcode.CreateListNFloats => OpcodeArgType.Float,
                Opcode.CreateListNStrings => OpcodeArgType.String,
                Opcode.CreateListNRefs => OpcodeArgType.Reference,
                Opcode.CreateListNResources => OpcodeArgType.Resource,
                Opcode.NPushFloatAssign => OpcodeArgType.Reference,
                _ => OpcodeArgType.Int
            };
        }

        private static bool IsPushLocal(byte[] bytecode, int pc, out int index)
        {
            index = 0;
            if (pc + 5 >= bytecode.Length) return false;
            if (bytecode[pc] == (byte)Opcode.PushReferenceValue && bytecode[pc + 1] == (byte)DMReference.Type.Local)
            {
                index = BitConverter.ToInt32(bytecode, pc + 2);
                return true;
            }
            return false;
        }

        private static void PreScanJumpTargets(byte[] bytecode, HashSet<int> targets)
        {
            int pc = 0;
            while (pc < bytecode.Length)
            {
                Opcode opcode = (Opcode)bytecode[pc++];
                var metadata = OpcodeMetadataCache.GetMetadata(opcode);
                if (metadata.RequiredArgs == null) continue;

                int varCount = 0;
                foreach (var argType in metadata.RequiredArgs)
                {
                    if (argType == OpcodeArgType.Label)
                    {
                        int target = BitConverter.ToInt32(bytecode, pc);
                        targets.Add(target);
                        pc += 4;
                    }
                    else
                    {
                        int size = GetArgSize(argType, bytecode, pc);
                        if (size == 4 && metadata.VariableArgs) varCount = BitConverter.ToInt32(bytecode, pc);
                        pc += size;
                    }
                }

                if (metadata.VariableArgs)
                {
                    var argType = GetVariableArgType(opcode);
                    for (int i = 0; i < varCount; i++)
                    {
                        pc += GetArgSize(argType, bytecode, pc);
                    }
                }
            }
        }

        private static bool IsPushArgument(byte[] bytecode, int pc, out int index)
        {
            index = 0;
            if (pc + 5 >= bytecode.Length) return false;
            if (bytecode[pc] == (byte)Opcode.PushReferenceValue && bytecode[pc + 1] == (byte)DMReference.Type.Argument)
            {
                index = BitConverter.ToInt32(bytecode, pc + 2);
                return true;
            }
            return false;
        }

        private static bool IsAssignLocal(byte[] bytecode, int pc, out int index)
        {
            index = 0;
            if (pc + 5 >= bytecode.Length) return false;
            if (bytecode[pc] == (byte)Opcode.Assign && bytecode[pc + 1] == (byte)DMReference.Type.Local)
            {
                index = BitConverter.ToInt32(bytecode, pc + 2);
                return true;
            }
            return false;
        }

        private static BuiltinVar? GetBuiltinVarType(string name)
        {
            return name switch
            {
                "icon" => BuiltinVar.Icon,
                "icon_state" => BuiltinVar.IconState,
                "dir" => BuiltinVar.Dir,
                "alpha" => BuiltinVar.Alpha,
                "color" => BuiltinVar.Color,
                "layer" => BuiltinVar.Layer,
                "pixel_x" => BuiltinVar.PixelX,
                "pixel_y" => BuiltinVar.PixelY,
                _ => null
            };
        }
    }
}
