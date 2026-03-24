using Shared.Enums;
using System;
using System.Runtime.CompilerServices;
using Core.VM.Procs;
using Shared;

namespace Core.VM.Runtime;

public unsafe partial class BytecodeInterpreter
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformLocalFieldTransfer(ref InterpreterState state, int srcIdx, int nameId, int targetIdx, int pcForCache)
    {
        var objValue = state.Locals[srcIdx];
        if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
        {
            // Persistent Inline Cache: utilize opcode-relative addressing for fast property access
            ref var cache = ref state.Proc._inlineCache[pcForCache];
            if (cache.ObjectType == obj.ObjectType)
            {
                state.Locals[targetIdx] = obj.GetVariableDirect(cache.VariableIndex);
            }
            else
            {
                var name = state.Strings[nameId];
                int varIdx = obj.ObjectType?.GetVariableIndex(name) ?? -1;
                if (varIdx != -1)
                {
                    cache.ObjectType = obj.ObjectType;
                    cache.VariableIndex = varIdx;
                    state.Locals[targetIdx] = obj.GetVariableDirect(varIdx);
                }
                else state.Locals[targetIdx] = obj.GetVariable(name);
            }
        }
        else throw new ScriptRuntimeException($"Field access on null object: {state.Strings[nameId]}", state.Proc, pcForCache, state.Thread);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformGlobalJumpIfFalse(ref InterpreterState state, int globalIdx, int address, int pcForError)
    {
        if ((uint)globalIdx >= (uint)state.Globals.Count)
            throw new ScriptRuntimeException($"Invalid global index: {globalIdx}", state.Proc, pcForError, state.Thread);

        var val = state.Globals[globalIdx];
        if (val.IsFalse()) state.PC = address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformBooleanAnd(ref InterpreterState state, int jumpAddress)
    {
        var val = state.Stack[--state.StackPtr];
        if (val.IsFalse())
        {
            state.Push(val);
            state.PC = jumpAddress;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformBooleanOr(ref InterpreterState state, int jumpAddress)
    {
        var val = state.Stack[--state.StackPtr];
        if (!val.IsFalse())
        {
            state.Push(val);
            state.PC = jumpAddress;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformBooleanNot(ref InterpreterState state)
    {
        var val = state.Stack[state.StackPtr - 1];
        state.Stack[state.StackPtr - 1] = val.IsFalse() ? DreamValue.True : DreamValue.False;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformJumpIfFalse(ref InterpreterState state, int address)
    {
        var val = state.Stack[--state.StackPtr];
        if (val.IsFalse()) state.PC = address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformLocalJumpIfFalse(ref InterpreterState state, int idx, int address, int pcForError)
    {
        if ((uint)idx >= (uint)state.Locals.Length)
            throw new ScriptRuntimeException("Local index out of bounds", state.Proc, pcForError, state.Thread);

        var val = state.Locals[idx];
        if (val.IsFalse()) state.PC = address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformLocalJumpIfTrue(ref InterpreterState state, int idx, int address, int pcForError)
    {
        if ((uint)idx >= (uint)state.Locals.Length)
            throw new ScriptRuntimeException("Local index out of bounds", state.Proc, pcForError, state.Thread);

        var val = state.Locals[idx];
        if (!val.IsFalse()) state.PC = address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformAdd(ref InterpreterState state)
    {
        var b = state.Stack[--state.StackPtr];
        ref var a = ref state.Stack[state.StackPtr - 1];
        if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
            a = new DreamValue(a.UnsafeRawLong + b.UnsafeRawLong);
        else if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
            a = new DreamValue(a.RawDouble + b.RawDouble);
        else a = a + b;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformSubtract(ref InterpreterState state)
    {
        var b = state.Stack[--state.StackPtr];
        ref var a = ref state.Stack[state.StackPtr - 1];
        if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
            a = new DreamValue(a.UnsafeRawLong - b.UnsafeRawLong);
        else if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
            a = new DreamValue(a.RawDouble - b.RawDouble);
        else a = a - b;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformMultiply(ref InterpreterState state)
    {
        var b = state.Stack[--state.StackPtr];
        ref var a = ref state.Stack[state.StackPtr - 1];
        if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
            a = new DreamValue(a.UnsafeRawLong * b.UnsafeRawLong);
        else if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
            a = new DreamValue(a.RawDouble * b.RawDouble);
        else a = a * b;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformDivide(ref InterpreterState state)
    {
        var b = state.Stack[--state.StackPtr];
        ref var a = ref state.Stack[state.StackPtr - 1];
        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
        {
            double db = b.RawDouble;
            a = (db != 0) ? new DreamValue(a.RawDouble / db) : new DreamValue(0.0);
        }
        else a = a / b;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformCompareEquals(ref InterpreterState state)
    {
        var b = state.Stack[--state.StackPtr];
        ref var a = ref state.Stack[state.StackPtr - 1];
        a = (a == b) ? DreamValue.True : DreamValue.False;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformCompareNotEquals(ref InterpreterState state)
    {
        var b = state.Stack[--state.StackPtr];
        ref var a = ref state.Stack[state.StackPtr - 1];
        a = (a != b) ? DreamValue.True : DreamValue.False;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformCompareLessThan(ref InterpreterState state)
    {
        var b = state.Stack[--state.StackPtr];
        ref var a = ref state.Stack[state.StackPtr - 1];
        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
        {
            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer) a = (a.UnsafeRawLong < b.UnsafeRawLong) ? DreamValue.True : DreamValue.False;
            else a = (a.GetValueAsDouble() < b.GetValueAsDouble()) ? DreamValue.True : DreamValue.False;
        }
        else a = (a < b) ? DreamValue.True : DreamValue.False;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformCompareGreaterThan(ref InterpreterState state)
    {
        var b = state.Stack[--state.StackPtr];
        ref var a = ref state.Stack[state.StackPtr - 1];
        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
        {
            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer) a = (a.UnsafeRawLong > b.UnsafeRawLong) ? DreamValue.True : DreamValue.False;
            else a = (a.GetValueAsDouble() > b.GetValueAsDouble()) ? DreamValue.True : DreamValue.False;
        }
        else a = (a > b) ? DreamValue.True : DreamValue.False;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformCompareLessThanOrEqual(ref InterpreterState state)
    {
        var b = state.Stack[--state.StackPtr];
        ref var a = ref state.Stack[state.StackPtr - 1];
        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
        {
            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer) a = (a.UnsafeRawLong <= b.UnsafeRawLong) ? DreamValue.True : DreamValue.False;
            else a = (a.GetValueAsDouble() <= b.GetValueAsDouble()) ? DreamValue.True : DreamValue.False;
        }
        else a = (a <= b) ? DreamValue.True : DreamValue.False;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformCompareGreaterThanOrEqual(ref InterpreterState state)
    {
        var b = state.Stack[--state.StackPtr];
        ref var a = ref state.Stack[state.StackPtr - 1];
        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
        {
            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer) a = (a.UnsafeRawLong >= b.UnsafeRawLong) ? DreamValue.True : DreamValue.False;
            else a = (a.GetValueAsDouble() >= b.GetValueAsDouble()) ? DreamValue.True : DreamValue.False;
        }
        else a = (a >= b) ? DreamValue.True : DreamValue.False;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformLocalCompareLessThanFloatJumpIfFalse(ref InterpreterState state, int idx, double val, int address)
    {
        if (!(state.Locals[idx] < val)) state.PC = address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformLocalCompareGreaterThanFloatJumpIfFalse(ref InterpreterState state, int idx, double val, int address)
    {
        if (!(state.Locals[idx] > val)) state.PC = address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformLocalCompareLessThanOrEqualFloatJumpIfFalse(ref InterpreterState state, int idx, double val, int address)
    {
        if (!(state.Locals[idx] <= val)) state.PC = address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformLocalCompareGreaterThanOrEqualFloatJumpIfFalse(ref InterpreterState state, int idx, double val, int address)
    {
        if (!(state.Locals[idx] >= val)) state.PC = address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformLocalJumpIfFieldFalse(ref InterpreterState state, int idx, int nameId, int address, int pcForError)
    {
        var objValue = state.Locals[idx];
        if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
        {
            var name = state.Strings[nameId];
            var val = obj.GetVariable(name);
            if (val.IsFalse()) state.PC = address;
        }
        else throw new ScriptRuntimeException($"Field access on null object: {state.Strings[nameId]}", state.Proc, pcForError, state.Thread);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformLocalJumpIfFieldTrue(ref InterpreterState state, int idx, int nameId, int address, int pcForError)
    {
        var objValue = state.Locals[idx];
        if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
        {
            var name = state.Strings[nameId];
            var val = obj.GetVariable(name);
            if (!val.IsFalse()) state.PC = address;
        }
        else throw new ScriptRuntimeException($"Field access on null object: {state.Strings[nameId]}", state.Proc, pcForError, state.Thread);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformJumpIfTrueReference(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    int address = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    if (!state.Locals[idx].IsFalse()) state.PC = address;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    int address = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    if (!state.Arguments[idx].IsFalse()) state.PC = address;
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    int address = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.Thread._stackPtr = state.StackPtr;
                    var val = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                    if (!val.IsFalse()) state.PC = address;
                }
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformJumpIfFalseReference(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    int address = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    if (state.Locals[idx].IsFalse()) state.PC = address;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    int address = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    if (state.Arguments[idx].IsFalse()) state.PC = address;
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    int address = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.Thread._stackPtr = state.StackPtr;
                    var val = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                    if (val.IsFalse()) state.PC = address;
                }
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformLocalCompareEqualsJumpIfFalse(ref InterpreterState state, int idx1, int idx2, int address)
    {
        if (state.GetLocal(idx1) != state.GetLocal(idx2)) state.PC = address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformLocalCompareNotEqualsJumpIfFalse(ref InterpreterState state, int idx1, int idx2, int address)
    {
        if (state.GetLocal(idx1) == state.GetLocal(idx2)) state.PC = address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformLocalCompareLessThanJumpIfFalse(ref InterpreterState state, int idx1, int idx2, int address)
    {
        if (!(state.GetLocal(idx1) < state.GetLocal(idx2))) state.PC = address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformLocalCompareGreaterThanJumpIfFalse(ref InterpreterState state, int idx1, int idx2, int address)
    {
        if (!(state.GetLocal(idx1) > state.GetLocal(idx2))) state.PC = address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformLocalCompareLessThanOrEqualJumpIfFalse(ref InterpreterState state, int idx1, int idx2, int address)
    {
        if (!(state.GetLocal(idx1) <= state.GetLocal(idx2))) state.PC = address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformLocalCompareGreaterThanOrEqualJumpIfFalse(ref InterpreterState state, int idx1, int idx2, int address)
    {
        if (!(state.GetLocal(idx1) >= state.GetLocal(idx2))) state.PC = address;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformLocalPushDereferenceField(ref InterpreterState state, int idx, int nameId, int pcForCache)
    {
        var objValue = state.GetLocal(idx);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformLocalPushDereferenceCall(ref InterpreterState state, int idx, int nameId, int pcForCache, DMCallArgumentsType argType, int argStackDelta)
    {
        var objValue = state.GetLocal(idx);
        if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
        {
            IDreamProc? targetProc;
            ref var cache = ref state.Proc._inlineCache[pcForCache];
            if (cache.ObjectType == obj.ObjectType && cache.CachedProc != null)
            {
                targetProc = cache.CachedProc;
            }
            else
            {
                var procName = state.Strings[nameId];
                targetProc = obj.ObjectType?.GetProc(procName);
                if (targetProc == null)
                {
                    var varValue = obj.GetVariable(procName);
                    if (varValue.TryGetValue(out IDreamProc? procFromVar)) targetProc = procFromVar;
                }
                if (targetProc != null)
                {
                    cache.ObjectType = obj.ObjectType;
                    cache.CachedProc = targetProc;
                }
            }

            if (targetProc != null)
            {
                state.Thread.SavePC(state.PC);
                int argCount = argStackDelta;
                state.Thread._stackPtr = state.StackPtr;
                state.Thread.PerformCall(targetProc, obj, argCount, argCount);
                return;
            }
        }
        state.StackPtr -= argStackDelta;
        state.Push(DreamValue.Null);
    }
}
