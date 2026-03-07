using Shared.Enums;
using System;
using System.Collections.Generic;
using Shared;

namespace Core.VM.Utils
{
    /// <summary>
    /// Provides tools for optimizing Dream VM bytecode by fusing common instruction patterns into super-instructions
    /// and pre-calculating jump targets.
    /// </summary>
    public static class BytecodeOptimizer
    {
        [ThreadStatic]
        private static List<byte>? _optimizationBuffer;
        [ThreadStatic]
        private static List<int>? _labelLocations;
        [ThreadStatic]
        private static int[]? _pcMap;
        [ThreadStatic]
        private static HashSet<int>? _jumpTargets;

        /// <summary>
        /// Optimizes the provided bytecode array.
        /// </summary>
        /// <param name="bytecode">The original bytecode to optimize.</param>
        /// <param name="strings">Optional string list for built-in variable optimization.</param>
        /// <returns>The optimized bytecode array.</returns>
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
            _labelLocations.Clear();

            if (_pcMap == null || _pcMap.Length < bytecode.Length + 1)
                _pcMap = new int[Math.Max(1024, bytecode.Length + 1)];

            Array.Fill(_pcMap, -1);

            int pc = 0;

            while (pc < bytecode.Length)
            {
                if (TryOptimizePushLocalPattern(bytecode, ref pc, optimized, _labelLocations, _jumpTargets)) continue;
                if (TryOptimizePushArgumentPattern(bytecode, ref pc, optimized, _jumpTargets)) continue;
                if (TryOptimizeReturnPattern(bytecode, ref pc, optimized, _jumpTargets)) continue;
                if (TryOptimizeAssignLocalPattern(bytecode, ref pc, optimized, _jumpTargets)) continue;
                if (TryOptimizeBuiltinVarPattern(bytecode, ref pc, optimized, strings, _jumpTargets)) continue;
                if (TryOptimizeReferenceJumpPattern(bytecode, ref pc, optimized, _labelLocations, _jumpTargets)) continue;

                // Default: Copy opcode and arguments, tracking labels
                _pcMap[pc] = optimized.Count;
                Opcode opcode = (Opcode)bytecode[pc++];
                optimized.Add((byte)opcode);

                var metadata = OpcodeMetadataCache.GetMetadata(opcode);
                int varCount = 0;
                foreach (var argType in metadata.RequiredArgs)
                {
                    if (argType == OpcodeArgType.Label)
                    {
                        _labelLocations.Add(optimized.Count);
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
            foreach (int labelLoc in _labelLocations)
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

        private static bool TryOptimizePushLocalPattern(byte[] bytecode, ref int pc, List<byte> optimized, List<int> labelLocations, HashSet<int> jumpTargets)
        {
            if (!IsPushLocal(bytecode, pc, out int idx) || IsAnyPartJumpTarget(pc, 6, jumpTargets)) return false;

            int originalPc = pc;
            int nextPc = pc + 6;

            // Pattern: PushLocal(idx), PushLocal(idx2) ...
            if (IsPushLocal(bytecode, nextPc, out int idx2) && !jumpTargets.Contains(nextPc) && !IsAnyPartJumpTarget(nextPc, 6, jumpTargets))
            {
                int nextNextPc = nextPc + 6;
                if (nextNextPc < bytecode.Length && !jumpTargets.Contains(nextNextPc) && !IsAnyPartJumpTarget(nextNextPc, 1, jumpTargets))
                {
                    Opcode op = (Opcode)bytecode[nextNextPc];
                    Opcode superOp = op switch
                    {
                        Opcode.Add => Opcode.LocalPushLocalPushAdd,
                        Opcode.Subtract => Opcode.LocalPushLocalPushSub,
                        Opcode.Multiply => Opcode.LocalPushLocalPushMul,
                        Opcode.Divide => Opcode.LocalPushLocalPushDiv,
                        Opcode.CompareEquals => Opcode.LocalCompareEquals,
                        Opcode.CompareNotEquals => Opcode.LocalCompareNotEquals,
                        _ => Opcode.Error
                    };

                    if (superOp != Opcode.Error)
                    {
                        // Check for LocalCompareEqualsJumpIfFalse or LocalCompareNotEqualsJumpIfFalse
                        int jumpPc = nextNextPc + 1;
                        if ((superOp == Opcode.LocalCompareEquals || superOp == Opcode.LocalCompareNotEquals) &&
                            jumpPc + 5 <= bytecode.Length && !jumpTargets.Contains(jumpPc) && bytecode[jumpPc] == (byte)Opcode.JumpIfFalse && !IsAnyPartJumpTarget(jumpPc, 5, jumpTargets))
                        {
                            Opcode jumpSuperOp = superOp == Opcode.LocalCompareEquals ? Opcode.LocalCompareEqualsJumpIfFalse : Opcode.LocalCompareNotEqualsJumpIfFalse;
                            MarkPcMap(originalPc, jumpPc + 5, optimized.Count);
                            optimized.Add((byte)jumpSuperOp);
                            optimized.AddRange(BitConverter.GetBytes(idx));
                            optimized.AddRange(BitConverter.GetBytes(idx2));
                            labelLocations.Add(optimized.Count);
                            optimized.AddRange(bytecode.AsSpan(jumpPc + 1, 4));
                            pc = jumpPc + 5;
                            return true;
                        }

                        // Check for LocalMulAdd
                        if (superOp == Opcode.LocalPushLocalPushMul)
                        {
                            int thirdPc = nextNextPc + 1;
                            if (IsPushLocal(bytecode, thirdPc, out int idx3) && !jumpTargets.Contains(thirdPc) && !IsAnyPartJumpTarget(thirdPc, 6, jumpTargets))
                            {
                                int fourthPc = thirdPc + 6;
                                if (fourthPc < bytecode.Length && !jumpTargets.Contains(fourthPc) && bytecode[fourthPc] == (byte)Opcode.Add && !IsAnyPartJumpTarget(fourthPc, 1, jumpTargets))
                                {
                                    MarkPcMap(originalPc, fourthPc + 1, optimized.Count);
                                    optimized.Add((byte)Opcode.LocalMulAdd);
                                    optimized.AddRange(BitConverter.GetBytes(idx));
                                    optimized.AddRange(BitConverter.GetBytes(idx2));
                                    optimized.AddRange(BitConverter.GetBytes(idx3));
                                    pc = fourthPc + 1;
                                    return true;
                                }
                            }
                        }

                        MarkPcMap(originalPc, nextNextPc + 1, optimized.Count);
                        optimized.Add((byte)superOp);
                        optimized.AddRange(BitConverter.GetBytes(idx));
                        optimized.AddRange(BitConverter.GetBytes(idx2));
                        pc = nextNextPc + 1;
                        return true;
                    }
                }
            }

            // Pattern: PushLocal(idx), PushFloat(val), Add
            if (nextPc + 9 < bytecode.Length && !jumpTargets.Contains(nextPc) && bytecode[nextPc] == (byte)Opcode.PushFloat && !IsAnyPartJumpTarget(nextPc, 9, jumpTargets))
            {
                int addPc = nextPc + 9;
                if (addPc < bytecode.Length && !jumpTargets.Contains(addPc) && bytecode[addPc] == (byte)Opcode.Add && !IsAnyPartJumpTarget(addPc, 1, jumpTargets))
                {
                    MarkPcMap(originalPc, addPc + 1, optimized.Count);
                    optimized.Add((byte)Opcode.LocalAddFloat);
                    optimized.AddRange(BitConverter.GetBytes(idx));
                    optimized.AddRange(bytecode.AsSpan(nextPc + 1, 8)); // Copy float
                    pc = addPc + 1;
                    return true;
                }
            }

            // --- 3-instruction patterns (check first) ---

            // Pattern: PushLocal(idx), BooleanNot, JumpIfFalse -> LocalJumpIfTrue
            if (nextPc + 1 + 5 <= bytecode.Length && !jumpTargets.Contains(nextPc) && bytecode[nextPc] == (byte)Opcode.BooleanNot && !jumpTargets.Contains(nextPc + 1) && bytecode[nextPc + 1] == (byte)Opcode.JumpIfFalse && !IsAnyPartJumpTarget(nextPc + 1, 5, jumpTargets))
            {
                MarkPcMap(originalPc, nextPc + 6, optimized.Count);
                optimized.Add((byte)Opcode.LocalJumpIfTrue);
                optimized.AddRange(BitConverter.GetBytes(idx));
                labelLocations.Add(optimized.Count);
                optimized.AddRange(bytecode.AsSpan(nextPc + 2, 4));
                pc = nextPc + 6;
                return true;
            }

            // Pattern: PushLocal(idx), IsNull, JumpIfFalse -> LocalJumpIfNotNull
            if (nextPc + 1 + 5 <= bytecode.Length && !jumpTargets.Contains(nextPc) && bytecode[nextPc] == (byte)Opcode.IsNull && !jumpTargets.Contains(nextPc + 1) && bytecode[nextPc + 1] == (byte)Opcode.JumpIfFalse && !IsAnyPartJumpTarget(nextPc + 1, 5, jumpTargets))
            {
                MarkPcMap(originalPc, nextPc + 6, optimized.Count);
                optimized.Add((byte)Opcode.LocalJumpIfNotNull);
                optimized.AddRange(BitConverter.GetBytes(idx));
                labelLocations.Add(optimized.Count);
                optimized.AddRange(bytecode.AsSpan(nextPc + 2, 4));
                pc = nextPc + 6;
                return true;
            }

            // --- 2-instruction patterns ---

            // Pattern: PushLocal(idx), JumpIfNull
            if (nextPc + 5 <= bytecode.Length && !jumpTargets.Contains(nextPc) && bytecode[nextPc] == (byte)Opcode.JumpIfNull && !IsAnyPartJumpTarget(nextPc, 5, jumpTargets))
            {
                MarkPcMap(originalPc, nextPc + 5, optimized.Count);
                optimized.Add((byte)Opcode.LocalJumpIfNull);
                optimized.AddRange(BitConverter.GetBytes(idx));
                labelLocations.Add(optimized.Count);
                optimized.AddRange(bytecode.AsSpan(nextPc + 1, 4));
                pc = nextPc + 5;
                return true;
            }

            // Pattern: PushLocal(idx), JumpIfFalse
            if (nextPc + 5 <= bytecode.Length && !jumpTargets.Contains(nextPc) && bytecode[nextPc] == (byte)Opcode.JumpIfFalse && !IsAnyPartJumpTarget(nextPc, 5, jumpTargets))
            {
                MarkPcMap(originalPc, nextPc + 5, optimized.Count);
                optimized.Add((byte)Opcode.LocalJumpIfFalse);
                optimized.AddRange(BitConverter.GetBytes(idx));
                labelLocations.Add(optimized.Count);
                optimized.AddRange(bytecode.AsSpan(nextPc + 1, 4));
                pc = nextPc + 5;
                return true;
            }

            // Pattern: PushLocal(idx), DereferenceCall
            if (nextPc + 9 < bytecode.Length && !jumpTargets.Contains(nextPc) && bytecode[nextPc] == (byte)Opcode.DereferenceCall && !IsAnyPartJumpTarget(nextPc, 10, jumpTargets))
            {
                int nameId = BitConverter.ToInt32(bytecode, nextPc + 1);
                var argType = bytecode[nextPc + 5];
                int argDelta = BitConverter.ToInt32(bytecode, nextPc + 6);

                MarkPcMap(originalPc, nextPc + 10, optimized.Count);
                optimized.Add((byte)Opcode.LocalPushDereferenceCall);
                optimized.AddRange(BitConverter.GetBytes(idx));
                optimized.AddRange(BitConverter.GetBytes(nameId));
                optimized.Add(argType);
                optimized.AddRange(BitConverter.GetBytes(argDelta));
                pc = nextPc + 10;
                return true;
            }

            // Pattern: PushLocal(idx), DereferenceField
            if (nextPc + 5 <= bytecode.Length && !jumpTargets.Contains(nextPc) && bytecode[nextPc] == (byte)Opcode.DereferenceField && !IsAnyPartJumpTarget(nextPc, 5, jumpTargets))
            {
                int nameId = BitConverter.ToInt32(bytecode, nextPc + 1);
                MarkPcMap(originalPc, nextPc + 5, optimized.Count);
                optimized.Add((byte)Opcode.LocalPushDereferenceField);
                optimized.AddRange(BitConverter.GetBytes(idx));
                optimized.AddRange(BitConverter.GetBytes(nameId));
                pc = nextPc + 5;
                return true;
            }

            // Pattern: PushLocal(idx), Return
            if (nextPc < bytecode.Length && !jumpTargets.Contains(nextPc) && bytecode[nextPc] == (byte)Opcode.Return)
            {
                MarkPcMap(originalPc, nextPc + 1, optimized.Count);
                optimized.Add((byte)Opcode.LocalPushReturn);
                optimized.AddRange(BitConverter.GetBytes(idx));
                pc = nextPc + 1;
                return true;
            }

            // Otherwise just optimize to PushLocal
            MarkPcMap(originalPc, originalPc + 6, optimized.Count);
            optimized.Add((byte)Opcode.PushLocal);
            optimized.AddRange(BitConverter.GetBytes(idx));
            pc += 6;
            return true;
        }

        private static bool TryOptimizePushArgumentPattern(byte[] bytecode, ref int pc, List<byte> optimized, HashSet<int> jumpTargets)
        {
            if (IsPushArgument(bytecode, pc, out int argIdx))
            {
                MarkPcMap(pc, pc + 6, optimized.Count);
                optimized.Add((byte)Opcode.PushArgument);
                optimized.AddRange(BitConverter.GetBytes(argIdx));
                pc += 6;
                return true;
            }

            if (pc + 1 < bytecode.Length && (bytecode[pc] == (byte)Opcode.Increment || bytecode[pc] == (byte)Opcode.Decrement))
            {
                if (bytecode[pc + 1] == (byte)DMReference.Type.Local && !IsAnyPartJumpTarget(pc, 6, jumpTargets))
                {
                    int lIdx = BitConverter.ToInt32(bytecode, pc + 2);
                    Opcode superOp = bytecode[pc] == (byte)Opcode.Increment ? Opcode.LocalIncrement : Opcode.LocalDecrement;
                    MarkPcMap(pc, pc + 6, optimized.Count);
                    optimized.Add((byte)superOp);
                    optimized.AddRange(BitConverter.GetBytes(lIdx));
                    pc += 6;
                    return true;
                }
            }
            return false;
        }

        private static bool TryOptimizeReturnPattern(byte[] bytecode, ref int pc, List<byte> optimized, HashSet<int> jumpTargets)
        {
            if (bytecode[pc] == (byte)Opcode.Return) return false;

            if (bytecode[pc] == (byte)Opcode.PushNull)
            {
                if (pc + 1 < bytecode.Length && bytecode[pc + 1] == (byte)Opcode.Return && !IsAnyPartJumpTarget(pc + 1, 1, jumpTargets))
                {
                    MarkPcMap(pc, pc + 2, optimized.Count);
                    optimized.Add((byte)Opcode.ReturnNull);
                    pc += 2;
                    return true;
                }
            }
            else if (bytecode[pc] == (byte)Opcode.PushFloat)
            {
                if (pc + 9 < bytecode.Length && bytecode[pc + 9] == (byte)Opcode.Return && !IsAnyPartJumpTarget(pc + 9, 1, jumpTargets))
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
            }
            return false;
        }

        private static bool TryOptimizeAssignLocalPattern(byte[] bytecode, ref int pc, List<byte> optimized, HashSet<int> jumpTargets)
        {
            if (IsAssignLocal(bytecode, pc, out int assignIdx))
            {
                MarkPcMap(pc, pc + 6, optimized.Count);
                optimized.Add((byte)Opcode.AssignLocal);
                optimized.AddRange(BitConverter.GetBytes(assignIdx));
                pc += 6;
                return true;
            }
            return false;
        }

        private static bool TryOptimizeBuiltinVarPattern(byte[] bytecode, ref int pc, List<byte> optimized, IReadOnlyList<string>? strings, HashSet<int> jumpTargets)
        {
            if (strings == null) return false;

            if (pc + 4 < bytecode.Length && (bytecode[pc] == (byte)Opcode.GetVariable || bytecode[pc] == (byte)Opcode.SetVariable))
            {
                int stringId = BitConverter.ToInt32(bytecode, pc + 1);
                if (stringId >= 0 && stringId < strings.Count)
                {
                    var builtin = GetBuiltinVarType(strings[stringId]);
                    if (builtin.HasValue)
                    {
                        Opcode superOp = bytecode[pc] == (byte)Opcode.GetVariable ? Opcode.GetBuiltinVar : Opcode.SetBuiltinVar;
                        MarkPcMap(pc, pc + 5, optimized.Count);
                        optimized.Add((byte)superOp);
                        optimized.Add((byte)builtin.Value);
                        pc += 5;
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool TryOptimizeReferenceJumpPattern(byte[] bytecode, ref int pc, List<byte> optimized, List<int> labelLocations, HashSet<int> jumpTargets)
        {
            // Super-super-optimization: LocalCompareEquals + JumpIfFalse -> LocalCompareEqualsJumpIfFalse
            if (bytecode[pc] == (byte)Opcode.LocalCompareEquals && !IsAnyPartJumpTarget(pc, 9, jumpTargets))
            {
                int nextPc = pc + 9;
                if (nextPc + 5 <= bytecode.Length && !jumpTargets.Contains(nextPc) && bytecode[nextPc] == (byte)Opcode.JumpIfFalse && !IsAnyPartJumpTarget(nextPc, 5, jumpTargets))
                {
                    int idx1 = BitConverter.ToInt32(bytecode, pc + 1);
                    int idx2 = BitConverter.ToInt32(bytecode, pc + 5);
                    MarkPcMap(pc, nextPc + 5, optimized.Count);
                    optimized.Add((byte)Opcode.LocalCompareEqualsJumpIfFalse);
                    optimized.AddRange(BitConverter.GetBytes(idx1));
                    optimized.AddRange(BitConverter.GetBytes(idx2));
                    labelLocations.Add(optimized.Count);
                    optimized.AddRange(bytecode.AsSpan(nextPc + 1, 4));
                    pc = nextPc + 5;
                    return true;
                }
            }

            if (bytecode[pc] == (byte)Opcode.LocalCompareNotEquals && !IsAnyPartJumpTarget(pc, 9, jumpTargets))
            {
                int nextPc = pc + 9;
                if (nextPc + 5 <= bytecode.Length && !jumpTargets.Contains(nextPc) && bytecode[nextPc] == (byte)Opcode.JumpIfFalse && !IsAnyPartJumpTarget(nextPc, 5, jumpTargets))
                {
                    int idx1 = BitConverter.ToInt32(bytecode, pc + 1);
                    int idx2 = BitConverter.ToInt32(bytecode, pc + 5);
                    MarkPcMap(pc, nextPc + 5, optimized.Count);
                    optimized.Add((byte)Opcode.LocalCompareNotEqualsJumpIfFalse);
                    optimized.AddRange(BitConverter.GetBytes(idx1));
                    optimized.AddRange(BitConverter.GetBytes(idx2));
                    labelLocations.Add(optimized.Count);
                    optimized.AddRange(bytecode.AsSpan(nextPc + 1, 4));
                    pc = nextPc + 5;
                    return true;
                }
            }

            // Pattern: PushReferenceValue(ref), JumpIfFalse(label) -> JumpIfReferenceFalse(ref, label)
            if (pc + 1 < bytecode.Length && bytecode[pc] == (byte)Opcode.PushReferenceValue && !IsAnyPartJumpTarget(pc, 1 + GetReferenceSize(bytecode, pc + 1), jumpTargets))
            {
                int refSize = GetReferenceSize(bytecode, pc + 1);
                int jumpPc = pc + 1 + refSize;
                if (jumpPc + 4 < bytecode.Length && bytecode[jumpPc] == (byte)Opcode.JumpIfFalse && !IsAnyPartJumpTarget(jumpPc, 5, jumpTargets))
                {
                    if (bytecode[pc + 1] == (byte)DMReference.Type.Local)
                    {
                        int lIdx = BitConverter.ToInt32(bytecode, pc + 2);
                        MarkPcMap(pc, jumpPc + 5, optimized.Count);
                        optimized.Add((byte)Opcode.LocalJumpIfFalse);
                        optimized.AddRange(BitConverter.GetBytes(lIdx));
                        labelLocations.Add(optimized.Count);
                        optimized.AddRange(bytecode.AsSpan(jumpPc + 1, 4));
                    }
                    else
                    {
                        MarkPcMap(pc, jumpPc + 5, optimized.Count);
                        optimized.Add((byte)Opcode.JumpIfReferenceFalse);
                        optimized.AddRange(bytecode.AsSpan(pc + 1, refSize));
                        labelLocations.Add(optimized.Count);
                        optimized.AddRange(bytecode.AsSpan(jumpPc + 1, 4));
                    }
                    pc = jumpPc + 5;
                    return true;
                }
            }

            // Pattern: JumpIfTrueReference(Local, ...) -> LocalJumpIfTrue
            if (bytecode[pc] == (byte)Opcode.JumpIfTrueReference && pc + 1 < bytecode.Length && bytecode[pc + 1] == (byte)DMReference.Type.Local)
            {
                int lIdx = BitConverter.ToInt32(bytecode, pc + 2);
                MarkPcMap(pc, pc + 10, optimized.Count);
                optimized.Add((byte)Opcode.LocalJumpIfTrue);
                optimized.AddRange(BitConverter.GetBytes(lIdx));
                labelLocations.Add(optimized.Count);
                optimized.AddRange(bytecode.AsSpan(pc + 6, 4));
                pc += 10;
                return true;
            }

            // Pattern: JumpIfFalseReference(Local, ...) -> LocalJumpIfFalse
            if (bytecode[pc] == (byte)Opcode.JumpIfFalseReference && pc + 1 < bytecode.Length && bytecode[pc + 1] == (byte)DMReference.Type.Local)
            {
                int lIdx = BitConverter.ToInt32(bytecode, pc + 2);
                MarkPcMap(pc, pc + 10, optimized.Count);
                optimized.Add((byte)Opcode.LocalJumpIfFalse);
                optimized.AddRange(BitConverter.GetBytes(lIdx));
                labelLocations.Add(optimized.Count);
                optimized.AddRange(bytecode.AsSpan(pc + 6, 4));
                pc += 10;
                return true;
            }

            // Pattern: JumpIfReferenceFalse(Local, ...) -> LocalJumpIfFalse
            if (bytecode[pc] == (byte)Opcode.JumpIfReferenceFalse && pc + 1 < bytecode.Length && bytecode[pc + 1] == (byte)DMReference.Type.Local)
            {
                int lIdx = BitConverter.ToInt32(bytecode, pc + 2);
                MarkPcMap(pc, pc + 10, optimized.Count);
                optimized.Add((byte)Opcode.LocalJumpIfFalse);
                optimized.AddRange(BitConverter.GetBytes(lIdx));
                labelLocations.Add(optimized.Count);
                optimized.AddRange(bytecode.AsSpan(pc + 6, 4));
                pc += 10;
                return true;
            }
            return false;
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

        private static bool IsAnyPartJumpTarget(int pc, int count, HashSet<int>? jumpTargets)
        {
            if (jumpTargets == null) return false;
            for (int i = 1; i < count; i++) // i=1 because jumping to the START of a pattern is okay
            {
                if (jumpTargets.Contains(pc + i)) return true;
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
