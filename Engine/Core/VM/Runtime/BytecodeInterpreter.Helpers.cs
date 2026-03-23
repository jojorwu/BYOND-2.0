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
}
