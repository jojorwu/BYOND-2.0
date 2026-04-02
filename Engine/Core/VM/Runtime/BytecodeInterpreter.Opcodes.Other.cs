using Shared.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Core.VM.Procs;
using Shared;

namespace Core.VM.Runtime;

public unsafe partial class BytecodeInterpreter
{
    private static void HandleGetVariable(ref InterpreterState state)
    {
        int id = state.ReadInt32();
        int pcForCache = state.PC - 5;
        var instance = state.Frame.Instance;
        DreamValue val = DreamValue.Null;
        if (instance != null)
        {
            // Persistent Inline Cache: utilize opcode-relative addressing for fast property access
            ref var cache = ref state.Proc._inlineCache[pcForCache];
            if (cache.ObjectType == instance.ObjectType)
            {
                val = instance.GetVariableDirect(cache.VariableIndex);
            }
            else
            {
                var name = state.Strings[id];
                int idx = instance.ObjectType?.GetVariableIndex(name) ?? -1;
                if (idx != -1)
                {
                    cache.ObjectType = instance.ObjectType;
                    cache.VariableIndex = idx;
                    val = instance.GetVariableDirect(idx);
                }
                else val = instance.GetVariable(name);
            }
        }
        state.Push(val);
    }

    private static void HandleSetVariable(ref InterpreterState state)
    {
        int id = state.ReadInt32();
        int pcForCache = state.PC - 5;
        var val = state.Pop();
        var instance = state.Frame.Instance;
        if (instance != null)
        {
            ref var cache = ref state.Proc._inlineCache[pcForCache];
            if (cache.ObjectType == instance.ObjectType)
            {
                instance.SetVariableDirect(cache.VariableIndex, val);
            }
            else
            {
                var name = state.Strings[id];
                int idx = instance.ObjectType?.GetVariableIndex(name) ?? -1;
                if (idx != -1)
                {
                    cache.ObjectType = instance.ObjectType;
                    cache.VariableIndex = idx;
                    instance.SetVariableDirect(idx, val);
                }
                else instance.SetVariable(name, val);
            }
        }
    }

    private static void HandlePushReferenceValue(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.Push(state.GetLocal(idx));
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.Push(state.GetArgument(idx));
                }
                break;
            case DMReference.Type.Global:
                {
                    int globalIdx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.Push(state.Thread.Context!.GetGlobal(globalIdx));
                }
                break;
            case DMReference.Type.Src:
                state.Push(state.Frame.Instance != null ? new DreamValue(state.Frame.Instance) : DreamValue.Null);
                break;
            case DMReference.Type.World:
                state.Push(state.Thread.Context!.World != null ? new DreamValue(state.Thread.Context.World) : DreamValue.Null);
                break;
            case DMReference.Type.SrcField:
                {
                    int nameId = *(int*)(state.BytecodePtr + state.PC);
                    int pcForCache = state.PC - 1;
                    state.PC += 4;
                    var instance = state.Frame.Instance;
                    if (instance != null)
                    {
                        ref var cache = ref state.Proc._inlineCache[pcForCache];
                        if (cache.ObjectType == instance.ObjectType)
                        {
                            state.Push(instance.GetVariableDirect(cache.VariableIndex));
                        }
                        else
                        {
                            var name = state.Strings[nameId];
                            int varIdx = instance.ObjectType?.GetVariableIndex(name) ?? -1;
                            if (varIdx != -1)
                            {
                                cache.ObjectType = instance.ObjectType;
                                cache.VariableIndex = varIdx;
                                state.Push(instance.GetVariableDirect(varIdx));
                            }
                            else state.Push(instance.GetVariable(name));
                        }
                    }
                    else state.Push(DreamValue.Null);
                }
                break;
            case DMReference.Type.Field:
                {
                    int nameId = *(int*)(state.BytecodePtr + state.PC);
                    int pcForCache = state.PC - 1;
                    state.PC += 4;
                    var objValue = state.Pop();
                    DreamValue val = DreamValue.Null;
                    if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
                    {
                        ref var cache = ref state.Proc._inlineCache[pcForCache];
                        if (cache.ObjectType == obj.ObjectType)
                        {
                            val = obj.GetVariableDirect(cache.VariableIndex);
                        }
                        else
                        {
                            var name = state.Strings[nameId];
                            int varIdx = obj.ObjectType?.GetVariableIndex(name) ?? -1;
                            if (varIdx != -1)
                            {
                                cache.ObjectType = obj.ObjectType;
                                cache.VariableIndex = varIdx;
                                val = obj.GetVariableDirect(varIdx);
                            }
                            else val = obj.GetVariable(name);
                        }
                    }
                    state.Push(val);
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var val = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                    state.Push(val);
                }
                break;
        }
    }

    private static void HandlePushGlobalVars(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_PushGlobalVars();
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleAsType(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during AsType", state.Proc, state.PC, state.Thread);
        var typeValue = state.Pop();
        ref var objValue = ref state.Peek();
        bool matches = false;
        if (objValue.Type == DreamValueType.DreamObject && typeValue.Type == DreamValueType.DreamType)
        {
            var obj = objValue.GetValueAsDreamObject();
            typeValue.TryGetValue(out ObjectType? type);
            if (obj?.ObjectType != null && type != null) matches = obj.ObjectType.IsSubtypeOf(type);
        }
        objValue = matches ? objValue : DreamValue.Null;
    }

    private static readonly ThreadLocal<System.Text.StringBuilder> _formatStringBuilder = new(() => new System.Text.StringBuilder(256));

    private static void HandlePushRefAndDereferenceField(ref InterpreterState state)
    {
        var reference = state.ReadReference();
        var fieldNameId = state.ReadInt32();
        var fieldName = state.Strings[fieldNameId];
        state.Thread._stackPtr = state.StackPtr;
        var objValue = state.Thread.GetReferenceValue(reference, ref state.Frame);
        DreamValue val = DreamValue.Null;
        if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj != null) val = obj.GetVariable(fieldName);
        state.StackPtr = state.Thread._stackPtr;
        state.Push(val);
    }

    private static void HandlePushLocal(ref InterpreterState state)
    {
        int idx = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;
        state.Push(state.GetLocal(idx));
    }

    private static void HandlePushLocal0(ref InterpreterState state) => state.Push(state.GetLocal(0));
    private static void HandlePushLocal1(ref InterpreterState state) => state.Push(state.GetLocal(1));
    private static void HandlePushLocal2(ref InterpreterState state) => state.Push(state.GetLocal(2));
    private static void HandlePushLocal3(ref InterpreterState state) => state.Push(state.GetLocal(3));
    private static void HandlePushLocal4(ref InterpreterState state) => state.Push(state.GetLocal(4));
    private static void HandlePushLocal5(ref InterpreterState state) => state.Push(state.GetLocal(5));
    private static void HandlePushLocal6(ref InterpreterState state) => state.Push(state.GetLocal(6));
    private static void HandlePushLocal7(ref InterpreterState state) => state.Push(state.GetLocal(7));
    private static void HandlePushLocal8(ref InterpreterState state) => state.Push(state.GetLocal(8));
    private static void HandlePushLocal9(ref InterpreterState state) => state.Push(state.GetLocal(9));
    private static void HandlePushLocal10(ref InterpreterState state) => state.Push(state.GetLocal(10));
    private static void HandlePushLocal11(ref InterpreterState state) => state.Push(state.GetLocal(11));
    private static void HandlePushLocal12(ref InterpreterState state) => state.Push(state.GetLocal(12));
    private static void HandlePushLocal13(ref InterpreterState state) => state.Push(state.GetLocal(13));
    private static void HandlePushLocal14(ref InterpreterState state) => state.Push(state.GetLocal(14));
    private static void HandlePushLocal15(ref InterpreterState state) => state.Push(state.GetLocal(15));

    private static void HandleAssignLocal0(ref InterpreterState state) => state.GetLocal(0) = state.Peek();
    private static void HandleAssignLocal1(ref InterpreterState state) => state.GetLocal(1) = state.Peek();
    private static void HandleAssignLocal2(ref InterpreterState state) => state.GetLocal(2) = state.Peek();
    private static void HandleAssignLocal3(ref InterpreterState state) => state.GetLocal(3) = state.Peek();
    private static void HandleAssignLocal4(ref InterpreterState state) => state.GetLocal(4) = state.Peek();
    private static void HandleAssignLocal5(ref InterpreterState state) => state.GetLocal(5) = state.Peek();
    private static void HandleAssignLocal6(ref InterpreterState state) => state.GetLocal(6) = state.Peek();
    private static void HandleAssignLocal7(ref InterpreterState state) => state.GetLocal(7) = state.Peek();
    private static void HandleAssignLocal8(ref InterpreterState state) => state.GetLocal(8) = state.Peek();
    private static void HandleAssignLocal9(ref InterpreterState state) => state.GetLocal(9) = state.Peek();
    private static void HandleAssignLocal10(ref InterpreterState state) => state.GetLocal(10) = state.Peek();
    private static void HandleAssignLocal11(ref InterpreterState state) => state.GetLocal(11) = state.Peek();
    private static void HandleAssignLocal12(ref InterpreterState state) => state.GetLocal(12) = state.Peek();
    private static void HandleAssignLocal13(ref InterpreterState state) => state.GetLocal(13) = state.Peek();
    private static void HandleAssignLocal14(ref InterpreterState state) => state.GetLocal(14) = state.Peek();
    private static void HandleAssignLocal15(ref InterpreterState state) => state.GetLocal(15) = state.Peek();

    private static void HandlePushArgument(ref InterpreterState state)
    {
        int idx = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;
        state.Push(state.GetArgument(idx));
    }

    private static void HandleGetBuiltinVar(ref InterpreterState state)
    {
        var varType = (BuiltinVar)state.ReadByte();
        var instance = state.Frame.Instance as GameObject;
        if (instance != null)
        {
            switch (varType)
            {
                case BuiltinVar.Icon: state.Push(new DreamValue(instance.Icon)); break;
                case BuiltinVar.IconState: state.Push(new DreamValue(instance.IconState)); break;
                case BuiltinVar.Dir: state.Push(new DreamValue((double)instance.Dir)); break;
                case BuiltinVar.Alpha: state.Push(new DreamValue(instance.Alpha)); break;
                case BuiltinVar.Color: state.Push(new DreamValue(instance.Color)); break;
                case BuiltinVar.Layer: state.Push(new DreamValue(instance.Layer)); break;
                case BuiltinVar.PixelX: state.Push(new DreamValue(instance.PixelX)); break;
                case BuiltinVar.PixelY: state.Push(new DreamValue(instance.PixelY)); break;
                default: state.Push(DreamValue.Null); break;
            }
        }
        else
        {
            state.Push(DreamValue.Null);
        }
    }

    private static void HandleSetBuiltinVar(ref InterpreterState state)
    {
        var varType = (BuiltinVar)state.ReadByte();
        var val = state.Pop();
        var instance = state.Frame.Instance as GameObject;
        if (instance != null)
        {
            switch (varType)
            {
                case BuiltinVar.Icon: val.TryGetValue(out string? s); if (s != null) instance.Icon = s; break;
                case BuiltinVar.IconState: val.TryGetValue(out string? s2); if (s2 != null) instance.IconState = s2; break;
                case BuiltinVar.Dir: instance.Dir = (int)val.GetValueAsFloat(); break;
                case BuiltinVar.Alpha: instance.Alpha = val.GetValueAsFloat(); break;
                case BuiltinVar.Color: val.TryGetValue(out string? s3); if (s3 != null) instance.Color = s3; break;
                case BuiltinVar.Layer: instance.Layer = val.GetValueAsFloat(); break;
                case BuiltinVar.PixelX: instance.PixelX = val.GetValueAsFloat(); break;
                case BuiltinVar.PixelY: instance.PixelY = val.GetValueAsFloat(); break;
            }
        }
    }

}
