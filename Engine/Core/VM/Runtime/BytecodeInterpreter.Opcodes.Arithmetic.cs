using Shared.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Core.VM.Procs;
using Shared;

namespace Core.VM.Runtime;

public unsafe partial class BytecodeInterpreter
{
    private static void HandleAdd(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Add", state.Proc, state.PC, state.Thread);
        PerformAdd(ref state);
    }

    private static void HandleSubtract(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Subtract", state.Proc, state.PC, state.Thread);
        PerformSubtract(ref state);
    }

    private static void HandleMultiply(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Multiply", state.Proc, state.PC, state.Thread);
        PerformMultiply(ref state);
    }

    private static void HandleDivide(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Divide", state.Proc, state.PC, state.Thread);
        PerformDivide(ref state);
    }

    private static void HandleNegate(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during Negate", state.Proc, state.PC, state.Thread);
        ref var a = ref state.Peek();
        if (a.Type <= DreamValueType.Integer)
        {
            if (a.Type == DreamValueType.Integer)
                a = new DreamValue(-a.UnsafeRawLong);
            else
                a = new DreamValue(-a.UnsafeRawDouble);
        }
        else
            a = -a;
    }

    private static void HandleBitAnd(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during BitAnd", state.Proc, state.PC, state.Thread);
        var b = state.Pop();
        ref var a = ref state.Peek();
        a = new DreamValue(a.RawLong & b.RawLong);
    }

    private static void HandleBitOr(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during BitOr", state.Proc, state.PC, state.Thread);
        var b = state.Pop();
        ref var a = ref state.Peek();
        a = new DreamValue(a.RawLong | b.RawLong);
    }

    private static void HandleBitXor(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during BitXor", state.Proc, state.PC, state.Thread);
        var b = state.Pop();
        ref var a = ref state.Peek();
        a = new DreamValue(a.RawLong ^ b.RawLong);
    }

    private static void HandleBitXorReference(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        var value = state.Pop();
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var l = ref state.GetLocal(idx);
                    l = l ^ value;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var arg = ref state.GetArgument(idx);
                    arg = arg ^ value;
                }
                break;
            case DMReference.Type.Global:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Thread.Context!.GetGlobal(idx);
                    state.Thread.Context.SetGlobal(idx, val ^ value);
                }
                break;
            case DMReference.Type.SrcField:
                {
                    var nameId = state.ReadInt32();
                    if (state.Frame.Instance is GameObject gameObject)
                    {
                        var name = state.Thread.Context!.Strings[nameId];
                        int idx = gameObject.ObjectType?.GetVariableIndex(name) ?? -1;
                        var val = idx != -1 ? gameObject.GetVariableDirect(idx) : gameObject.GetVariable(name);
                        var newVal = val ^ value;
                        if (idx != -1) gameObject.SetVariableDirect(idx, newVal);
                        else gameObject.SetVariable(name, newVal);
                    }
                    else if (state.Frame.Instance != null)
                    {
                        var name = state.Thread.Context!.Strings[nameId];
                        var val = state.Frame.Instance.GetVariable(name);
                        state.Frame.Instance.SetVariable(name, val ^ value);
                    }
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var refValue = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.SetReferenceValue(reference, ref state.Frame, refValue ^ value, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                }
                break;
        }
    }

    private static void HandleBitNot(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during BitNot", state.Proc, state.PC, state.Thread);
        ref var a = ref state.Peek();
        a = new DreamValue(~a.RawLong);
    }

    private static void HandleBitShiftLeft(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during BitShiftLeft", state.Proc, state.PC, state.Thread);
        var b = state.Pop();
        ref var a = ref state.Peek();
        a = new DreamValue(SharedOperations.BitShiftLeft(a.RawLong, b.RawLong));
    }

    private static void HandleBitShiftLeftReference(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        var value = state.Pop();
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var l = ref state.GetLocal(idx);
                    l = l << value;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var arg = ref state.GetArgument(idx);
                    arg = arg << value;
                }
                break;
            case DMReference.Type.Global:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Thread.Context!.GetGlobal(idx);
                    state.Thread.Context.SetGlobal(idx, val << value);
                }
                break;
            case DMReference.Type.SrcField:
                {
                    var nameId = state.ReadInt32();
                    if (state.Frame.Instance is GameObject gameObject)
                    {
                        var name = state.Thread.Context!.Strings[nameId];
                        int idx = gameObject.ObjectType?.GetVariableIndex(name) ?? -1;
                        var val = idx != -1 ? gameObject.GetVariableDirect(idx) : gameObject.GetVariable(name);
                        var newVal = val << value;
                        if (idx != -1) gameObject.SetVariableDirect(idx, newVal);
                        else gameObject.SetVariable(name, newVal);
                    }
                    else if (state.Frame.Instance != null)
                    {
                        var name = state.Thread.Context!.Strings[nameId];
                        var val = state.Frame.Instance.GetVariable(name);
                        state.Frame.Instance.SetVariable(name, val << value);
                    }
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var refValue = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.SetReferenceValue(reference, ref state.Frame, refValue << value, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                }
                break;
        }
    }

    private static void HandleBitShiftRight(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during BitShiftRight", state.Proc, state.PC, state.Thread);
        var b = state.Pop();
        ref var a = ref state.Peek();
        a = new DreamValue(SharedOperations.BitShiftRight(a.RawLong, b.RawLong));
    }

    private static void HandleBitShiftRightReference(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        var value = state.Pop();
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var l = ref state.GetLocal(idx);
                    l = l >> value;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var arg = ref state.GetArgument(idx);
                    arg = arg >> value;
                }
                break;
            case DMReference.Type.Global:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Thread.Context!.GetGlobal(idx);
                    state.Thread.Context.SetGlobal(idx, val >> value);
                }
                break;
            case DMReference.Type.SrcField:
                {
                    var nameId = state.ReadInt32();
                    if (state.Frame.Instance is GameObject gameObject)
                    {
                        var name = state.Thread.Context!.Strings[nameId];
                        int idx = gameObject.ObjectType?.GetVariableIndex(name) ?? -1;
                        var val = idx != -1 ? gameObject.GetVariableDirect(idx) : gameObject.GetVariable(name);
                        var newVal = val >> value;
                        if (idx != -1) gameObject.SetVariableDirect(idx, newVal);
                        else gameObject.SetVariable(name, newVal);
                    }
                    else if (state.Frame.Instance != null)
                    {
                        var name = state.Thread.Context!.Strings[nameId];
                        var val = state.Frame.Instance.GetVariable(name);
                        state.Frame.Instance.SetVariable(name, val >> value);
                    }
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var refValue = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.SetReferenceValue(reference, ref state.Frame, refValue >> value, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                }
                break;
        }
    }

    private static void HandleModulus(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Modulus", state.Proc, state.PC, state.Thread);
        var b = state.Pop();
        ref var a = ref state.Peek();
        a = a % b;
    }

    private static void HandleModulusReference(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        var value = state.Pop();
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var val = ref state.GetLocal(idx);
                    if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                    {
                        double db = value.UnsafeRawDouble;
                        val = (db != 0) ? new DreamValue(val.UnsafeRawDouble % db) : DreamValue.False;
                    }
                    else val = val % value;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var val = ref state.GetArgument(idx);
                    if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                    {
                        double db = value.UnsafeRawDouble;
                        val = (db != 0) ? new DreamValue(val.UnsafeRawDouble % db) : DreamValue.False;
                    }
                    else val = val % value;
                }
                break;
            case DMReference.Type.Global:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Thread.Context!.GetGlobal(idx);
                    if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                    {
                        double db = value.UnsafeRawDouble;
                        state.Thread.Context.SetGlobal(idx, (db != 0) ? new DreamValue(val.UnsafeRawDouble % db) : DreamValue.False);
                    }
                    else state.Thread.Context.SetGlobal(idx, val % value);
                }
                break;
            case DMReference.Type.SrcField:
                {
                    var nameId = state.ReadInt32();
                    if (state.Frame.Instance is GameObject gameObject)
                    {
                        var name = state.Thread.Context!.Strings[nameId];
                        int idx = gameObject.ObjectType?.GetVariableIndex(name) ?? -1;
                        var val = idx != -1 ? gameObject.GetVariableDirect(idx) : gameObject.GetVariable(name);
                        DreamValue newVal;
                        if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                        {
                            double db = value.UnsafeRawDouble;
                            newVal = (db != 0) ? new DreamValue(val.UnsafeRawDouble % db) : DreamValue.False;
                        }
                        else newVal = val % value;
                        if (idx != -1) gameObject.SetVariableDirect(idx, newVal);
                        else gameObject.SetVariable(name, newVal);
                    }
                    else if (state.Frame.Instance != null)
                    {
                        var name = state.Thread.Context!.Strings[nameId];
                        var val = state.Frame.Instance.GetVariable(name);
                        state.Frame.Instance.SetVariable(name, val % value);
                    }
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var refValue = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.SetReferenceValue(reference, ref state.Frame, refValue % value, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                }
                break;
        }
    }

    private static void HandleModulusModulus(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during ModulusModulus", state.Proc, state.PC, state.Thread);
        var b = state.Pop();
        ref var a = ref state.Peek();
        a = new DreamValue(SharedOperations.Modulo(a.GetValueAsDouble(), b.GetValueAsDouble()));
    }

    private static void HandleModulusModulusReference(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        var value = state.Pop();
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var val = ref state.GetLocal(idx);
                    double da = val.UnsafeRawDouble;
                    double db = value.UnsafeRawDouble;
                    val = (db != 0) ? new DreamValue(da - db * Math.Floor(da / db)) : DreamValue.False;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var val = ref state.GetArgument(idx);
                    double da = val.UnsafeRawDouble;
                    double db = value.UnsafeRawDouble;
                    val = (db != 0) ? new DreamValue(da - db * Math.Floor(da / db)) : DreamValue.False;
                }
                break;
            case DMReference.Type.Global:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Thread.Context!.GetGlobal(idx);
                    double da = val.UnsafeRawDouble;
                    double db = value.UnsafeRawDouble;
                    state.Thread.Context.SetGlobal(idx, (db != 0) ? new DreamValue(da - db * Math.Floor(da / db)) : DreamValue.False);
                }
                break;
            case DMReference.Type.SrcField:
                {
                    var nameId = state.ReadInt32();
                    if (state.Frame.Instance is GameObject gameObject)
                    {
                        var name = state.Thread.Context!.Strings[nameId];
                        int idx = gameObject.ObjectType?.GetVariableIndex(name) ?? -1;
                        var val = idx != -1 ? gameObject.GetVariableDirect(idx) : gameObject.GetVariable(name);
                        double da = val.UnsafeRawDouble;
                        double db = value.UnsafeRawDouble;
                        var newVal = (db != 0) ? new DreamValue(da - db * Math.Floor(da / db)) : DreamValue.False;
                        if (idx != -1) gameObject.SetVariableDirect(idx, newVal);
                        else gameObject.SetVariable(name, newVal);
                    }
                    else if (state.Frame.Instance != null)
                    {
                        var name = state.Thread.Context!.Strings[nameId];
                        var val = state.Frame.Instance.GetVariable(name);
                        state.Frame.Instance.SetVariable(name, new DreamValue(SharedOperations.Modulo(val.GetValueAsDouble(), value.GetValueAsDouble())));
                    }
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var refValue = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.SetReferenceValue(reference, ref state.Frame, new DreamValue(SharedOperations.Modulo(refValue.GetValueAsFloat(), value.GetValueAsFloat())), 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                }
                break;
        }
    }

    private static void HandlePower(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Power", state.Proc, state.PC, state.Thread);
        var b = state.Pop();
        ref var a = ref state.Peek();
        double da = a.GetValueAsDouble();
        double db = b.GetValueAsDouble();

        // Optimized fast-paths for common powers
        if (db == 2.0) a = new DreamValue(da * da);
        else if (db == 0.5) a = new DreamValue(Math.Sqrt(da));
        else if (db == 1.0) { /* a stays a */ }
        else if (db == 0.0) a = DreamValue.True;
        else a = new DreamValue(Math.Pow(da, db));
    }

    private static void HandleSqrt(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during Sqrt", state.Proc, state.PC, state.Thread);
        ref var a = ref state.Peek();
        a = new DreamValue(Math.Sqrt(a.GetValueAsDouble()));
    }

    private static void HandleAbs(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during Abs", state.Proc, state.PC, state.Thread);
        ref var a = ref state.Peek();
        a = new DreamValue(Math.Abs(a.GetValueAsDouble()));
    }

    private static void HandleLocalAddConst(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        double val = state.ReadDouble();
        ref var a = ref state.GetLocal(idx);
        if (a.Type <= DreamValueType.Integer) a = new DreamValue(a.UnsafeRawDouble + val);
        else a = a + val;
    }

    private static void HandleLocalSubConst(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        double val = state.ReadDouble();
        ref var a = ref state.GetLocal(idx);
        if (a.Type <= DreamValueType.Integer) a = new DreamValue(a.UnsafeRawDouble - val);
        else a = a - val;
    }

    private static void HandleMultiplyReference(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.ReadByte();
        var value = state.Pop();
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = state.ReadInt32();
                    ref var val = ref state.GetLocal(idx);
                    if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                    {
                        if (val.Type == DreamValueType.Integer && value.Type == DreamValueType.Integer)
                            val = new DreamValue(val.UnsafeRawLong * value.UnsafeRawLong);
                        else
                            val = new DreamValue(val.UnsafeRawDouble * value.UnsafeRawDouble);
                    }
                    else
                        val = val * value;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = state.ReadInt32();
                    ref var val = ref state.GetArgument(idx);
                    if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                    {
                        if (val.Type == DreamValueType.Integer && value.Type == DreamValueType.Integer)
                            val = new DreamValue(val.UnsafeRawLong * value.UnsafeRawLong);
                        else
                            val = new DreamValue(val.UnsafeRawDouble * value.UnsafeRawDouble);
                    }
                    else
                        val = val * value;
                }
                break;
            case DMReference.Type.Global:
                {
                    int idx = state.ReadInt32();
                    var val = state.Thread.Context!.GetGlobal(idx);
                    if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                    {
                        if (val.Type == DreamValueType.Integer && value.Type == DreamValueType.Integer)
                            state.Thread.Context.SetGlobal(idx, new DreamValue(val.UnsafeRawLong * value.UnsafeRawLong));
                        else
                            state.Thread.Context.SetGlobal(idx, new DreamValue(val.UnsafeRawDouble * value.UnsafeRawDouble));
                    }
                    else
                        state.Thread.Context.SetGlobal(idx, val * value);
                }
                break;
            case DMReference.Type.SrcField:
                {
                    var nameId = state.ReadInt32();
                    if (state.Frame.Instance is GameObject gameObject)
                    {
                        var name = state.Thread.Context!.Strings[nameId];
                        int idx = gameObject.ObjectType?.GetVariableIndex(name) ?? -1;
                        var val = idx != -1 ? gameObject.GetVariableDirect(idx) : gameObject.GetVariable(name);
                        DreamValue newVal;
                        if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                        {
                            if (val.Type == DreamValueType.Integer && value.Type == DreamValueType.Integer)
                                newVal = new DreamValue(val.UnsafeRawLong * value.UnsafeRawLong);
                            else
                                newVal = new DreamValue(val.UnsafeRawDouble * value.UnsafeRawDouble);
                        }
                        else
                            newVal = val * value;
                        if (idx != -1) gameObject.SetVariableDirect(idx, newVal);
                        else gameObject.SetVariable(name, newVal);
                    }
                    else if (state.Frame.Instance != null)
                    {
                        var name = state.Thread.Context!.Strings[nameId];
                        var val = state.Frame.Instance.GetVariable(name);
                        state.Frame.Instance.SetVariable(name, val * value);
                    }
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var refValue = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.SetReferenceValue(reference, ref state.Frame, refValue * value, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                }
                break;
        }
    }

    private static void HandleSin(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during Sin", state.Proc, state.PC, state.Thread);
        ref var a = ref state.Peek();
        a = new DreamValue(Math.Sin(a.GetValueAsDouble() * (Math.PI / 180.0)));
    }

    private static void HandleDivideReference(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.ReadByte();
        var value = state.Pop();
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = state.ReadInt32();
                    ref var val = ref state.GetLocal(idx);
                    if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                    {
                        double dv = value.UnsafeRawDouble;
                        val = (dv != 0) ? new DreamValue(val.UnsafeRawDouble / dv) : new DreamValue(0.0);
                    }
                    else
                        val = val / value;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = state.ReadInt32();
                    ref var val = ref state.GetArgument(idx);
                    if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                    {
                        double dv = value.UnsafeRawDouble;
                        val = (dv != 0) ? new DreamValue(val.UnsafeRawDouble / dv) : new DreamValue(0.0);
                    }
                    else
                        val = val / value;
                }
                break;
            case DMReference.Type.Global:
                {
                    int idx = state.ReadInt32();
                    var val = state.Thread.Context!.GetGlobal(idx);
                    if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                    {
                        double dv = value.UnsafeRawDouble;
                        state.Thread.Context.SetGlobal(idx, (dv != 0) ? new DreamValue(val.UnsafeRawDouble / dv) : new DreamValue(0.0));
                    }
                    else
                        state.Thread.Context.SetGlobal(idx, val / value);
                }
                break;
            case DMReference.Type.SrcField:
                {
                    var nameId = state.ReadInt32();
                    if (state.Frame.Instance is GameObject gameObject)
                    {
                        var name = state.Thread.Context!.Strings[nameId];
                        int idx = gameObject.ObjectType?.GetVariableIndex(name) ?? -1;
                        var val = idx != -1 ? gameObject.GetVariableDirect(idx) : gameObject.GetVariable(name);
                        DreamValue newVal;
                        if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                        {
                            double dv = value.UnsafeRawDouble;
                            newVal = (dv != 0) ? new DreamValue(val.UnsafeRawDouble / dv) : new DreamValue(0.0);
                        }
                        else
                            newVal = val / value;
                        if (idx != -1) gameObject.SetVariableDirect(idx, newVal);
                        else gameObject.SetVariable(name, newVal);
                    }
                    else if (state.Frame.Instance != null)
                    {
                        var name = state.Thread.Context!.Strings[nameId];
                        var val = state.Frame.Instance.GetVariable(name);
                        state.Frame.Instance.SetVariable(name, val / value);
                    }
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var refValue = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.SetReferenceValue(reference, ref state.Frame, refValue / value, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                }
                break;
        }
    }

    private static void HandleCos(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during Cos", state.Proc, state.PC, state.Thread);
        ref var a = ref state.Peek();
        a = new DreamValue(Math.Cos(a.GetValueAsDouble() * (Math.PI / 180.0)));
    }

    private static void HandleTan(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during Tan", state.Proc, state.PC, state.Thread);
        ref var a = ref state.Peek();
        a = new DreamValue(Math.Tan(a.GetValueAsDouble() * (Math.PI / 180.0)));
    }

    private static void HandleArcSin(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during ArcSin", state.Proc, state.PC, state.Thread);
        ref var a = ref state.Peek();
        a = new DreamValue(Math.Asin(a.GetValueAsDouble()) * (180.0 / Math.PI));
    }

    private static void HandleArcCos(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during ArcCos", state.Proc, state.PC, state.Thread);
        ref var a = ref state.Peek();
        a = new DreamValue(Math.Acos(a.GetValueAsDouble()) * (180.0 / Math.PI));
    }

    private static void HandleArcTan(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during ArcTan", state.Proc, state.PC, state.Thread);
        ref var a = ref state.Peek();
        a = new DreamValue(Math.Atan(a.GetValueAsDouble()) * (180.0 / Math.PI));
    }

    private static void HandleArcTan2(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during ArcTan2", state.Proc, state.PC, state.Thread);
        var y = state.Pop();
        ref var x = ref state.Peek();
        x = new DreamValue(Math.Atan2(y.GetValueAsDouble(), x.GetValueAsDouble()) * (180.0 / Math.PI));
    }

    private static void HandleLog(ref InterpreterState state)
    {
        var baseValue = state.Pop();
        var x = state.Pop();
        state.Push(new DreamValue(Math.Log(x.GetValueAsDouble(), baseValue.GetValueAsDouble())));
    }

    private static void HandleLogE(ref InterpreterState state)
    {
        state.Push(new DreamValue(Math.Log(state.Pop().GetValueAsDouble())));
    }

}
