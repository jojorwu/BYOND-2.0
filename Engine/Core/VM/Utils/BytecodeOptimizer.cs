using System;
using System.Collections.Generic;
using Shared;

namespace Core.VM.Utils
{
    public static class BytecodeOptimizer
    {
        [ThreadStatic]
        private static List<byte>? _optimizationBuffer;
        [ThreadStatic]
        private static List<int>? _labelLocations;
        [ThreadStatic]
        private static int[]? _pcMap;

        public static byte[] Optimize(byte[] bytecode, IReadOnlyList<string>? strings = null)
        {
            if (bytecode == null || bytecode.Length == 0) return bytecode ?? Array.Empty<byte>();

            // Reclaim memory if buffers are excessively large (e.g., > 1MB)
            if (_optimizationBuffer != null && _optimizationBuffer.Capacity > 1024 * 1024) _optimizationBuffer = null;
            if (_pcMap != null && _pcMap.Length > 256 * 1024) _pcMap = null;

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
                int currentOriginalPc = pc;
                int currentOptimizedPc = optimized.Count;

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
                            MarkPcMap(pc, nextNextPc + 1, optimized.Count);
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
                                    MarkPcMap(pc, fourthPc + 1, optimized.Count);
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
                            MarkPcMap(pc, addPc + 1, optimized.Count);
                            optimized.Add((byte)Opcode.LocalAddFloat);
                            optimized.Add(idx);
                            optimized.AddRange(bytecode.AsSpan(pc + 4, 4)); // Copy float
                            pc = addPc + 1;
                            continue;
                        }
                    }

                    // Otherwise just optimize to PushLocal
                    MarkPcMap(pc, pc + 3, optimized.Count);
                    optimized.Add((byte)Opcode.PushLocal);
                    optimized.Add(idx);
                    pc += 3;
                    continue;
                }

                // Pattern: PushReferenceValue(Argument, idx)
                if (IsPushArgument(bytecode, pc, out byte argIdx))
                {
                    MarkPcMap(pc, pc + 3, optimized.Count);
                    optimized.Add((byte)Opcode.PushArgument);
                    optimized.Add(argIdx);
                    pc += 3;
                    continue;
                }

                // Pattern: Assign(Local, idx)
                if (IsAssignLocal(bytecode, pc, out byte assignIdx))
                {
                    MarkPcMap(pc, pc + 3, optimized.Count);
                    optimized.Add((byte)Opcode.AssignLocal);
                    optimized.Add(assignIdx);
                    pc += 3;
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

                // Pattern: PushReferenceValue(ref), JumpIfFalse(label) -> JumpIfReferenceFalse(ref, label)
                if (pc + 1 < bytecode.Length && bytecode[pc] == (byte)Opcode.PushReferenceValue)
                {
                    int refSize = GetReferenceSize(bytecode, pc + 1);
                    int jumpPc = pc + 1 + refSize;
                    if (jumpPc + 4 < bytecode.Length && bytecode[jumpPc] == (byte)Opcode.JumpIfFalse)
                    {
                        MarkPcMap(pc, jumpPc + 5, optimized.Count);
                        optimized.Add((byte)Opcode.JumpIfReferenceFalse);
                        optimized.AddRange(bytecode.AsSpan(pc + 1, refSize));
                        _labelLocations.Add(optimized.Count);
                        optimized.AddRange(bytecode.AsSpan(jumpPc + 1, 4));
                        pc = jumpPc + 5;
                        continue;
                    }
                }

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
                OpcodeArgType.Float => 4,
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
                DMReference.Type.Local => 2,
                DMReference.Type.Argument => 2,
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
