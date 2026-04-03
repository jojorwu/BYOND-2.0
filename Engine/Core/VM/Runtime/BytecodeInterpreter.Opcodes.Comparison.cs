using Shared.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Core.VM.Procs;
using Shared;

namespace Core.VM.Runtime;

public unsafe partial class BytecodeInterpreter
{
    private static void HandleCompareEquals(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareEquals", state.Proc, state.PC, state.Thread);
        PerformCompareEquals(ref state);
    }

    private static void HandleCompareNotEquals(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareNotEquals", state.Proc, state.PC, state.Thread);
        PerformCompareNotEquals(ref state);
    }

    private static void HandleCompareLessThan(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareLessThan", state.Proc, state.PC, state.Thread);
        PerformCompareLessThan(ref state);
    }

    private static void HandleCompareGreaterThan(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareGreaterThan", state.Proc, state.PC, state.Thread);
        PerformCompareGreaterThan(ref state);
    }

    private static void HandleCompareLessThanOrEqual(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareLessThanOrEqual", state.Proc, state.PC, state.Thread);
        PerformCompareLessThanOrEqual(ref state);
    }

    private static void HandleCompareGreaterThanOrEqual(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareGreaterThanOrEqual", state.Proc, state.PC, state.Thread);
        PerformCompareGreaterThanOrEqual(ref state);
    }

    private static void HandleCompareEquivalent(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareEquivalent", state.Proc, state.PC, state.Thread);
        var b = state.Pop();
        ref var a = ref state.Peek();
        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
        {
            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
                a = (a.UnsafeRawLong == b.UnsafeRawLong) ? DreamValue.True : DreamValue.False;
            else
                a = (a.UnsafeRawDouble == b.UnsafeRawDouble || Math.Abs(a.UnsafeRawDouble - b.UnsafeRawDouble) < 1e-5) ? DreamValue.True : DreamValue.False;
        }
        else
            a = a.Equals(b) ? DreamValue.True : DreamValue.False;
    }

    private static void HandleCompareNotEquivalent(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareNotEquivalent", state.Proc, state.PC, state.Thread);
        var b = state.Pop();
        ref var a = ref state.Peek();
        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
        {
            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
                a = (a.UnsafeRawLong != b.UnsafeRawLong) ? DreamValue.True : DreamValue.False;
            else
                a = (a.UnsafeRawDouble != b.UnsafeRawDouble && Math.Abs(a.UnsafeRawDouble - b.UnsafeRawDouble) >= 1e-5) ? DreamValue.True : DreamValue.False;
        }
        else
            a = !a.Equals(b) ? DreamValue.True : DreamValue.False;
    }

    private static void HandleIsNull(ref InterpreterState state)
    {
        ref var a = ref state.Peek();
        a = a.IsNull ? DreamValue.True : DreamValue.False;
    }

    private static void HandleIsType(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during IsType", state.Proc, state.PC, state.Thread);
        var typeValue = state.Pop();
        ref var objValue = ref state.Peek();
        bool result = false;
        if (objValue.Type == DreamValueType.DreamObject && typeValue.Type == DreamValueType.DreamType)
        {
            var obj = objValue.GetValueAsDreamObject();
            typeValue.TryGetValue(out ObjectType? type);
            if (obj?.ObjectType != null && type != null) result = obj.ObjectType.IsSubtypeOf(type);
        }
        objValue = result ? DreamValue.True : DreamValue.False;
    }

    private static void HandleIsInRange(ref InterpreterState state)
    {
        if (state.StackPtr < 3) throw new ScriptRuntimeException("Stack underflow during IsInRange", state.Proc, state.PC, state.Thread);
        var max = state.Pop();
        var min = state.Pop();
        var val = state.Pop();

        if (val.Type <= DreamValueType.Integer && min.Type <= DreamValueType.Integer && max.Type <= DreamValueType.Integer)
        {
            double dv = val.UnsafeRawDouble;
            state.Push(dv >= min.UnsafeRawDouble && dv <= max.UnsafeRawDouble ? DreamValue.True : DreamValue.False);
        }
        else
        {
            state.Push(val >= min && val <= max ? DreamValue.True : DreamValue.False);
        }
    }

    private static void HandleIsTypeDirect(ref InterpreterState state)
    {
        int typeId = state.ReadInt32();
        var value = state.Pop();
        bool result = false;
        if (value.Type == DreamValueType.DreamObject && value.TryGetValue(out DreamObject? obj) && obj != null)
        {
            var ot = obj.ObjectType;
            if (ot != null)
            {
                var targetType = state.Thread.Context!.ObjectTypeManager?.GetObjectType(typeId);
                if (targetType != null) result = ot.IsSubtypeOf(targetType);
            }
        }
        state.Push(result ? DreamValue.True : DreamValue.False);
    }

}
