using Shared.Enums;
using System;
using System.Collections.Generic;
using Shared;

namespace Core.VM.Utils;

public static class BytecodeOptimizer
{
    private const int PushLocalSize = 6;
    private const int AssignLocalSize = 6;
    private const int JumpSize = 5;
    private const int FloatSize = 9;
    private const int FieldSize = 5;
    private const int CallSize = 10;
    private const int PopSize = 1;
    private const int OpcodeSize = 1;
    private const int IntSize = 4;
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
            if (bytecode[pc] == (byte)Opcode.Pop && !IsJumpTarget(pc, PopSize))
            {
                int popCount = 1;
                int scanPc = pc + PopSize;
                while (scanPc < bytecode.Length && bytecode[scanPc] == (byte)Opcode.Pop && !IsJumpTarget(scanPc, PopSize))
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

            if (pc + PushLocalSize < bytecode.Length && bytecode[pc] == (byte)Opcode.Increment && bytecode[pc + 1] == (byte)DMReference.Type.Local && !IsJumpTarget(pc, PushLocalSize))
            {
                int idxInc = BitConverter.ToInt32(bytecode, pc + 2);
                if (pc + PushLocalSize < bytecode.Length && bytecode[pc + PushLocalSize] == (byte)Opcode.Pop && !IsJumpTarget(pc + PushLocalSize, PopSize))
                {
                    MarkPcMap(pc, pc + PushLocalSize + PopSize, optimized.Count);
                    optimized.Add((byte)Opcode.LocalIncrement);
                    optimized.AddRange(BitConverter.GetBytes(idxInc));
                    pc += PushLocalSize + PopSize;
                    continue;
                }
            }

            if (pc + PushLocalSize < bytecode.Length && bytecode[pc] == (byte)Opcode.Decrement && bytecode[pc + 1] == (byte)DMReference.Type.Local && !IsJumpTarget(pc, PushLocalSize))
            {
                int idxDec = BitConverter.ToInt32(bytecode, pc + 2);
                if (pc + PushLocalSize < bytecode.Length && bytecode[pc + PushLocalSize] == (byte)Opcode.Pop && !IsJumpTarget(pc + PushLocalSize, PopSize))
                {
                    MarkPcMap(pc, pc + PushLocalSize + PopSize, optimized.Count);
                    optimized.Add((byte)Opcode.LocalDecrement);
                    optimized.AddRange(BitConverter.GetBytes(idxDec));
                    pc += PushLocalSize + PopSize;
                    continue;
                }
            }

            if (TryOptimizeReturnPattern(bytecode, ref pc, optimized)) continue;

            if (IsPushArgument(bytecode, pc, out int argIdx))
            {
                MarkPcMap(pc, pc + PushLocalSize, optimized.Count);
                optimized.Add((byte)Opcode.PushArgument);
                optimized.AddRange(BitConverter.GetBytes(argIdx));
                pc += PushLocalSize;
                continue;
            }

            if (IsAssignLocal(bytecode, pc, out int assignIdx))
            {
                MarkPcMap(pc, pc + AssignLocalSize, optimized.Count);
                optimized.Add((byte)Opcode.AssignLocal);
                optimized.AddRange(BitConverter.GetBytes(assignIdx));
                pc += AssignLocalSize;
                continue;
            }

            if (strings != null && pc + IntSize < bytecode.Length && bytecode[pc] == (byte)Opcode.GetVariable)
            {
                int stringId = BitConverter.ToInt32(bytecode, pc + OpcodeSize);
                if (stringId >= 0 && stringId < strings.Count)
                {
                    var builtin = GetBuiltinVarType(strings[stringId]);
                    if (builtin.HasValue)
                    {
                        MarkPcMap(pc, pc + OpcodeSize + IntSize, optimized.Count);
                        optimized.Add((byte)Opcode.GetBuiltinVar);
                        optimized.Add((byte)builtin.Value);
                        pc += OpcodeSize + IntSize;
                        continue;
                    }
                }
            }

            if (strings != null && pc + IntSize < bytecode.Length && bytecode[pc] == (byte)Opcode.SetVariable)
            {
                int stringId = BitConverter.ToInt32(bytecode, pc + OpcodeSize);
                if (stringId >= 0 && stringId < strings.Count)
                {
                    var builtin = GetBuiltinVarType(strings[stringId]);
                    if (builtin.HasValue)
                    {
                        MarkPcMap(pc, pc + OpcodeSize + IntSize, optimized.Count);
                        optimized.Add((byte)Opcode.SetBuiltinVar);
                        optimized.Add((byte)builtin.Value);
                        pc += OpcodeSize + IntSize;
                        continue;
                    }
                }
            }

            if (pc + OpcodeSize < bytecode.Length && bytecode[pc] == (byte)Opcode.PushReferenceValue && !IsJumpTarget(pc, OpcodeSize + GetReferenceSize(bytecode, pc + OpcodeSize)))
            {
                int refSize = GetReferenceSize(bytecode, pc + OpcodeSize);
                int jumpPc = pc + OpcodeSize + refSize;
                if (jumpPc + (JumpSize - 1) < bytecode.Length && bytecode[jumpPc] == (byte)Opcode.JumpIfFalse && !IsJumpTarget(jumpPc, JumpSize))
                {
                    if (bytecode[pc + OpcodeSize] == (byte)DMReference.Type.Global)
                    {
                        MarkPcMap(pc, jumpPc + JumpSize, optimized.Count);
                        optimized.Add((byte)Opcode.GlobalJumpIfFalse);
                        optimized.AddRange(bytecode.AsSpan(pc + 2, IntSize));
                        labels.Add(optimized.Count);
                        optimized.AddRange(bytecode.AsSpan(jumpPc + 1, IntSize));
                        pc = jumpPc + JumpSize;
                        continue;
                    }
                    MarkPcMap(pc, jumpPc + JumpSize, optimized.Count);
                    optimized.Add((byte)Opcode.JumpIfReferenceFalse);
                    optimized.AddRange(bytecode.AsSpan(pc + 1, refSize));
                    labels.Add(optimized.Count);
                    optimized.AddRange(bytecode.AsSpan(jumpPc + 1, IntSize));
                    pc = jumpPc + JumpSize;
                    continue;
                }
            }

            _pcMap[pc] = optimized.Count;
            Opcode opcode = (Opcode)bytecode[pc++];
            optimized.Add((byte)opcode);
            var metadata = OpcodeMetadataCache.GetMetadata(opcode);
            if (metadata.RequiredArgs != null)
            {
                int varCount = 0;
                foreach (var argType in metadata.RequiredArgs)
                {
                    if (argType == OpcodeArgType.Label)
                    {
                        labels.Add(optimized.Count);
                        optimized.AddRange(bytecode.AsSpan(pc, IntSize));
                        pc += IntSize;
                    }
                    else
                    {
                        int size = GetArgSize(argType, bytecode, pc);
                        if (size == IntSize && metadata.VariableArgs) varCount = BitConverter.ToInt32(bytecode, pc);
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
        }
        _pcMap[pc] = optimized.Count;
        byte[] result = optimized.ToArray();
        foreach (int labelLoc in labels)
        {
            int originalTarget = BitConverter.ToInt32(result, labelLoc);
            if (originalTarget >= 0 && originalTarget < _pcMap.Length)
            {
                int optimizedTarget = _pcMap[originalTarget];
                if (optimizedTarget == -1)
                {
                    for (int i = originalTarget; i < _pcMap.Length; i++)
                    {
                        if (_pcMap[i] != -1) { optimizedTarget = _pcMap[i]; break; }
                    }
                }
                var targetBytes = BitConverter.GetBytes(optimizedTarget);
                for (int i = 0; i < IntSize; i++) result[labelLoc + i] = targetBytes[i];
            }
        }
        return result;
    }

    private static bool IsJumpTarget(int start, int count)
    {
        if (_jumpTargets == null) return false;
        for (int i = 1; i < count; i++)
            if (_jumpTargets.Contains(start + i)) return true;
        return false;
    }

    private static bool TryOptimizePushLocalPattern(byte[] bytecode, ref int pc, List<byte> optimized, IReadOnlyList<string>? strings, List<int> labels)
    {
        if (IsPushLocal(bytecode, pc, out int idx) && !IsJumpTarget(pc, PushLocalSize))
        {
            int nextPc = pc + PushLocalSize;
            if (IsPushLocal(bytecode, nextPc, out int idx2) && !IsJumpTarget(nextPc, PushLocalSize))
            {
                int nextNextPc = nextPc + PushLocalSize;
                if (nextNextPc < bytecode.Length && !IsJumpTarget(nextNextPc, OpcodeSize))
                {
                    Opcode op = (Opcode)bytecode[nextNextPc];
                    if (op == Opcode.CompareEquals || op == Opcode.CompareNotEquals || op == Opcode.CompareLessThan || op == Opcode.CompareGreaterThan || op == Opcode.CompareLessThanOrEqual || op == Opcode.CompareGreaterThanOrEqual)
                    {
                        int jumpPc = nextNextPc + OpcodeSize;
                        if (jumpPc + (JumpSize - 1) < bytecode.Length && bytecode[jumpPc] == (byte)Opcode.JumpIfFalse && !IsJumpTarget(jumpPc, JumpSize))
                        {
                            Opcode fused = op switch {
                                Opcode.CompareEquals => Opcode.LocalCompareEqualsJumpIfFalse,
                                Opcode.CompareNotEquals => Opcode.LocalCompareNotEqualsJumpIfFalse,
                                Opcode.CompareLessThan => Opcode.LocalCompareLessThanJumpIfFalse,
                                Opcode.CompareGreaterThan => Opcode.LocalCompareGreaterThanJumpIfFalse,
                                Opcode.CompareLessThanOrEqual => Opcode.LocalCompareLessThanOrEqualJumpIfFalse,
                                Opcode.CompareGreaterThanOrEqual => Opcode.LocalCompareGreaterThanOrEqualJumpIfFalse,
                                _ => throw new Exception()
                            };
                            MarkPcMap(pc, jumpPc + JumpSize, optimized.Count);
                            optimized.Add((byte)fused);
                            optimized.AddRange(BitConverter.GetBytes(idx));
                            optimized.AddRange(BitConverter.GetBytes(idx2));
                            labels.Add(optimized.Count);
                            optimized.AddRange(bytecode.AsSpan(jumpPc + 1, IntSize));
                            pc = jumpPc + JumpSize;
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    private static bool TryOptimizeReturnPattern(byte[] bytecode, ref int pc, List<byte> optimized)
    {
        if (pc + OpcodeSize < bytecode.Length && bytecode[pc] == (byte)Opcode.PushNull && bytecode[pc + OpcodeSize] == (byte)Opcode.Return && !IsJumpTarget(pc + OpcodeSize, OpcodeSize))
        {
            MarkPcMap(pc, pc + 2 * OpcodeSize, optimized.Count);
            optimized.Add((byte)Opcode.ReturnNull);
            pc += 2 * OpcodeSize;
            return true;
        }
        return false;
    }

    private static bool TryOptimizeArithmeticFloatPattern(byte[] bytecode, int pc, int floatPc, int opPc, int idx, Opcode assignOp, Opcode pushOp, List<byte> optimized, out int newPc)
    {
        newPc = 0; return false;
    }

    private static bool TryOptimizeArithmeticPattern(byte[] bytecode, int pc, int opPc, int idx1, int idx2, Opcode assignOp, Opcode pushOp, List<byte> optimized, out int newPc)
    {
        newPc = 0; return false;
    }

    private static void MarkPcMap(int start, int end, int optimizedPc)
    {
        for (int i = start; i < end; i++) _pcMap![i] = optimizedPc;
    }

    private static int GetArgSize(OpcodeArgType type, byte[] bytecode, int pc)
    {
        return type switch {
            OpcodeArgType.Label => 4, OpcodeArgType.Int => 4, OpcodeArgType.Float => 8,
            OpcodeArgType.String => 4, OpcodeArgType.Reference => GetReferenceSize(bytecode, pc),
            _ => 4
        };
    }

    private static int GetReferenceSize(byte[] bytecode, int pc)
    {
        if (pc >= bytecode.Length) return 0;
        var type = (DMReference.Type)bytecode[pc];
        return type switch {
            DMReference.Type.Global => 5, DMReference.Type.Local => 5, DMReference.Type.Argument => 5,
            DMReference.Type.Field => 5, _ => 1
        };
    }

    private static OpcodeArgType GetVariableArgType(Opcode opcode) => OpcodeArgType.Int;

    private static bool IsPushLocal(byte[] bytecode, int pc, out int index)
    {
        index = 0;
        if (pc + PushLocalSize - 1 >= bytecode.Length) return false;
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
            if (metadata.RequiredArgs != null)
            {
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
    }

    private static bool IsPushArgument(byte[] bytecode, int pc, out int index)
    {
        index = 0;
        if (pc + PushLocalSize - 1 >= bytecode.Length) return false;
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
        if (pc + AssignLocalSize - 1 >= bytecode.Length) return false;
        if (bytecode[pc] == (byte)Opcode.Assign && bytecode[pc + 1] == (byte)DMReference.Type.Local)
        {
            index = BitConverter.ToInt32(bytecode, pc + 2);
            return true;
        }
        return false;
    }

    private static BuiltinVar? GetBuiltinVarType(string name) => null;
}
