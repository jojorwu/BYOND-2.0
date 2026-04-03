using Shared.Enums;
using System;
using System.Collections.Generic;
using Shared;

namespace Core.VM.Utils
{
    public static class BytecodeVerifier
    {
        public static void Verify(byte[] bytecode, IReadOnlyList<string>? strings, int procCount, int typeCount)
        {
            if (bytecode == null || bytecode.Length == 0) return;

            int pc = 0;
            while (pc < bytecode.Length)
            {
                Opcode opcode = (Opcode)bytecode[pc++];
                var metadata = OpcodeMetadataCache.GetMetadata(opcode);

                // 1. Basic Opcode Validation
                if (metadata.RequiredArgs == null)
                {
                    // This might happen if an unknown byte is encountered that isn't in the Opcode enum
                    // but since bytecode is byte, and we cast it, we should check if it's a defined opcode.
                    if (!Enum.IsDefined(typeof(Opcode), opcode))
                        throw new System.Security.SecurityException($"Invalid opcode 0x{(byte)opcode:X2} at PC {pc - 1}");
                    continue;
                }

                int varCount = 0;
                foreach (var argType in metadata.RequiredArgs)
                {
                    pc = VerifyArg(bytecode, pc, argType, strings, procCount, typeCount, out var intVal);
                    if (metadata.VariableArgs && argType == OpcodeArgType.Int)
                    {
                        if (intVal < 0 || intVal > 1000000)
                            throw new System.Security.SecurityException($"Variable argument count {intVal} is too large at PC {pc - 4}");
                        varCount = intVal;
                    }
                }

                if (metadata.VariableArgs)
                {
                    var argType = GetVariableArgType(opcode);
                    for (int i = 0; i < varCount; i++)
                    {
                        pc = VerifyArg(bytecode, pc, argType, strings, procCount, typeCount, out _);
                    }
                }
            }
        }

        private static int VerifyArg(byte[] bytecode, int pc, OpcodeArgType type, IReadOnlyList<string>? strings, int procCount, int typeCount, out int intVal)
        {
            intVal = 0;
            switch (type)
            {
                case OpcodeArgType.Label:
                    if (pc + 4 > bytecode.Length) throw new System.Security.SecurityException($"Incomplete Label argument at PC {pc}");
                    intVal = BitConverter.ToInt32(bytecode, pc);
                    if (intVal < 0 || intVal >= bytecode.Length)
                        throw new System.Security.SecurityException($"Jump target {intVal} is out of bounds at PC {pc}");
                    return pc + 4;
                case OpcodeArgType.String:
                    if (pc + 4 > bytecode.Length) throw new System.Security.SecurityException($"Incomplete String argument at PC {pc}");
                    intVal = BitConverter.ToInt32(bytecode, pc);
                    if (strings != null && strings.Count > 0 && (intVal < 0 || intVal >= strings.Count))
                        throw new System.Security.SecurityException($"String index {intVal} is out of bounds at PC {pc}");
                    return pc + 4;
                case OpcodeArgType.ProcId:
                    if (pc + 4 > bytecode.Length) throw new System.Security.SecurityException($"Incomplete ProcId argument at PC {pc}");
                    intVal = BitConverter.ToInt32(bytecode, pc);
                    if (procCount > 0 && (intVal < 0 || intVal >= procCount))
                        throw new System.Security.SecurityException($"Proc index {intVal} is out of bounds at PC {pc}");
                    return pc + 4;
                case OpcodeArgType.TypeId:
                    if (pc + 4 > bytecode.Length) throw new System.Security.SecurityException($"Incomplete TypeId argument at PC {pc}");
                    intVal = BitConverter.ToInt32(bytecode, pc);
                    if (typeCount > 0 && (intVal < 0 || intVal >= typeCount))
                        throw new System.Security.SecurityException($"Type index {intVal} is out of bounds at PC {pc}");
                    return pc + 4;
                case OpcodeArgType.Reference:
                    return VerifyReference(bytecode, pc, strings);
                case OpcodeArgType.ArgType:
                    if (pc + 1 > bytecode.Length) throw new System.Security.SecurityException($"Incomplete ArgType at PC {pc}");
                    return pc + 1;
                case OpcodeArgType.StackDelta:
                    if (pc + 4 > bytecode.Length) throw new System.Security.SecurityException($"Incomplete StackDelta at PC {pc}");
                    return pc + 4;
                case OpcodeArgType.Resource:
                    if (pc + 4 > bytecode.Length) throw new System.Security.SecurityException($"Incomplete Resource at PC {pc}");
                    return pc + 4;
                case OpcodeArgType.EnumeratorId:
                    if (pc + 1 > bytecode.Length) throw new System.Security.SecurityException($"Incomplete EnumeratorId at PC {pc}");
                    return pc + 1;
                case OpcodeArgType.FilterId:
                    if (pc + 4 > bytecode.Length) throw new System.Security.SecurityException($"Incomplete FilterId at PC {pc}");
                    return pc + 4;
                case OpcodeArgType.ListSize:
                    if (pc + 4 > bytecode.Length) throw new System.Security.SecurityException($"Incomplete ListSize at PC {pc}");
                    return pc + 4;
                case OpcodeArgType.Int:
                    if (pc + 4 > bytecode.Length) throw new System.Security.SecurityException($"Incomplete Int argument at PC {pc}");
                    intVal = BitConverter.ToInt32(bytecode, pc);
                    return pc + 4;
                case OpcodeArgType.Float:
                    if (pc + 8 > bytecode.Length) throw new System.Security.SecurityException($"Incomplete Float argument at PC {pc}");
                    return pc + 8;
                case OpcodeArgType.FormatCount:
                    if (pc + 4 > bytecode.Length) throw new System.Security.SecurityException($"Incomplete FormatCount at PC {pc}");
                    return pc + 4;
                case OpcodeArgType.PickCount:
                    if (pc + 4 > bytecode.Length) throw new System.Security.SecurityException($"Incomplete PickCount at PC {pc}");
                    return pc + 4;
                case OpcodeArgType.ConcatCount:
                    if (pc + 4 > bytecode.Length) throw new System.Security.SecurityException($"Incomplete ConcatCount at PC {pc}");
                    return pc + 4;
                case OpcodeArgType.CacheIdx:
                    if (pc + 4 > bytecode.Length) throw new System.Security.SecurityException($"Incomplete CacheIdx at PC {pc}");
                    return pc + 4;
                default: return pc;
            }
        }

        private static int VerifyReference(byte[] bytecode, int pc, IReadOnlyList<string>? strings)
        {
            if (pc >= bytecode.Length) throw new System.Security.SecurityException($"Incomplete Reference at PC {pc}");
            var refType = (DMReference.Type)bytecode[pc++];
            switch (refType)
            {
                case DMReference.Type.Src:
                case DMReference.Type.Self:
                case DMReference.Type.Usr:
                case DMReference.Type.World:
                case DMReference.Type.Args:
                case DMReference.Type.ListIndex:
                    return pc;
                case DMReference.Type.Global:
                case DMReference.Type.Local:
                case DMReference.Type.Argument:
                    if (pc + 4 > bytecode.Length) throw new System.Security.SecurityException($"Incomplete Reference index at PC {pc}");
                    return pc + 4;
                case DMReference.Type.Field:
                case DMReference.Type.SrcField:
                case DMReference.Type.SrcProc:
                case DMReference.Type.GlobalProc:
                    if (pc + 4 > bytecode.Length) throw new System.Security.SecurityException($"Incomplete Reference string index at PC {pc}");
                    int stringId = BitConverter.ToInt32(bytecode, pc);
                    if (strings != null && (stringId < 0 || stringId >= strings.Count))
                        throw new System.Security.SecurityException($"Reference string index {stringId} is out of bounds at PC {pc}");
                    return pc + 4;
                default:
                    return pc;
            }
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
                Opcode.MassConcatenation => OpcodeArgType.Int,
                _ => OpcodeArgType.Int
            };
        }
    }
}
