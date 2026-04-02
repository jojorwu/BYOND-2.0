using Shared.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Core.VM.Procs;
using Shared;

namespace Core.VM.Runtime;

public unsafe partial class BytecodeInterpreter
{
    private static void HandleLocalFieldTransfer(ref InterpreterState state)
    {
        int pcForCache = state.PC - 1;
        int srcIdx = state.ReadInt32();
        int nameId = state.ReadInt32();
        int targetIdx = state.ReadInt32();
        PerformLocalFieldTransfer(ref state, srcIdx, nameId, targetIdx, pcForCache);
    }

    private static void HandleGlobalJumpIfFalse(ref InterpreterState state)
    {
        int pcForError = state.PC - 1;
        int globalIdx = state.ReadInt32();
        int address = state.ReadInt32();
        PerformGlobalJumpIfFalse(ref state, globalIdx, address, pcForError);
    }

    private static void HandleLocalCompareLessThanFloatJumpIfFalse(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        double val = state.ReadDouble();
        int address = state.ReadInt32();
        PerformLocalCompareLessThanFloatJumpIfFalse(ref state, idx, val, address);
    }

    private static void HandleLocalCompareGreaterThanFloatJumpIfFalse(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        double val = state.ReadDouble();
        int address = state.ReadInt32();
        PerformLocalCompareGreaterThanFloatJumpIfFalse(ref state, idx, val, address);
    }

    private static void HandleLocalCompareLessThanOrEqualFloatJumpIfFalse(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        double val = state.ReadDouble();
        int address = state.ReadInt32();
        PerformLocalCompareLessThanOrEqualFloatJumpIfFalse(ref state, idx, val, address);
    }

    private static void HandleLocalCompareGreaterThanOrEqualFloatJumpIfFalse(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        double val = state.ReadDouble();
        int address = state.ReadInt32();
        PerformLocalCompareGreaterThanOrEqualFloatJumpIfFalse(ref state, idx, val, address);
    }

    private static void HandleLocalJumpIfFieldFalse(ref InterpreterState state)
    {
        int pcForError = state.PC - 1;
        int idx = state.ReadInt32();
        int nameId = state.ReadInt32();
        int address = state.ReadInt32();
        PerformLocalJumpIfFieldFalse(ref state, idx, nameId, address, pcForError);
    }

    private static void HandleLocalJumpIfFieldTrue(ref InterpreterState state)
    {
        int pcForError = state.PC - 1;
        int idx = state.ReadInt32();
        int nameId = state.ReadInt32();
        int address = state.ReadInt32();
        PerformLocalJumpIfFieldTrue(ref state, idx, nameId, address, pcForError);
    }

    private static void HandleLocalPushDereferenceCall(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        int nameId = state.ReadInt32();
        int pcForCache = state.PC - 5;
        var argType = (DMCallArgumentsType)state.ReadByte();
        int argStackDelta = state.ReadInt32();
        PerformLocalPushDereferenceCall(ref state, idx, nameId, pcForCache, argType, argStackDelta);
    }

    private static void HandleLocalPushDereferenceIndex(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        var index = state.Pop();
        var objValue = state.GetLocal(idx);
        DreamValue val = DreamValue.Null;
        if (objValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
        {
            if (index.Type <= DreamValueType.Integer)
            {
                int i = (int)index.UnsafeRawDouble - 1;
                if (i >= 0 && i < list.Values.Count) val = list.Values[i];
            }
            else val = list.GetValue(index);
        }
        state.Push(val);
    }

    private static void HandleAssign(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        var value = state.Peek();
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.GetLocal(idx) = value;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.GetArgument(idx) = value;
                }
                break;
            case DMReference.Type.Global:
                {
                    int globalIdx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.Thread.Context!.SetGlobal(globalIdx, value);
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    var val = state.Pop();
                    state.Thread._stackPtr = state.StackPtr;
                    state.Thread.SetReferenceValue(reference, ref state.Frame, val, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                    state.Push(val);
                }
                break;
        }
    }

    private static void HandleIncrement(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var val = ref state.GetLocal(idx);
                    var newVal = val + 1;
                    val = newVal;
                    state.Push(newVal);
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var val = ref state.GetArgument(idx);
                    var newVal = val + 1;
                    val = newVal;
                    state.Push(newVal);
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var value = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    var newValue = value + 1;
                    state.Thread.SetReferenceValue(reference, ref state.Frame, newValue, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                    state.Push(newValue);
                }
                break;
        }
    }

    private static void HandleDecrement(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var val = ref state.GetLocal(idx);
                    var newVal = val - 1;
                    val = newVal;
                    state.Push(newVal);
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var val = ref state.GetArgument(idx);
                    var newVal = val - 1;
                    val = newVal;
                    state.Push(newVal);
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var value = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    var newValue = value - 1;
                    state.Thread.SetReferenceValue(reference, ref state.Frame, newValue, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                    state.Push(newValue);
                }
                break;
        }
    }

    private static void HandleAssignInto(ref InterpreterState state)
    {
        var reference = state.ReadReference();
        var value = state.Pop();
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.SetReferenceValue(reference, ref state.Frame, value, 0);
        state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
        state.StackPtr = state.Thread._stackPtr;
        state.Push(value);
    }

    private static void HandleAssignNoPush(ref InterpreterState state)
    {
        var reference = state.ReadReference();
        var value = state.Pop();
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.SetReferenceValue(reference, ref state.Frame, value, 0);
        state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandlePushNRefs(ref InterpreterState state)
    {
        int count = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;

        for (int i = 0; i < count; i++)
        {
            var reference = state.ReadReference();
            state.Thread._stackPtr = state.StackPtr;
            var val = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.StackPtr = state.Thread._stackPtr;
            state.Push(val);
        }
    }

    private static void HandleNullRef(ref InterpreterState state)
    {
        var reference = state.ReadReference();
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.SetReferenceValue(reference, ref state.Frame, DreamValue.Null, 0);
        state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleIndexRefWithString(ref InterpreterState state)
    {
        var reference = state.ReadReference();
        var stringId = state.ReadInt32();
        var stringValue = new DreamValue(state.Strings[stringId]);
        state.Thread._stackPtr = state.StackPtr;
        var objValue = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
        state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
        DreamValue result = DreamValue.Null;
        if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj is DreamList list) result = list.GetValue(stringValue);
        state.StackPtr = state.Thread._stackPtr;
        state.Push(result);
    }

    private static void HandleAssignLocal(ref InterpreterState state)
    {
        int idx = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;
        state.GetLocal(idx) = state.Peek();
    }

    private static void HandleLocalPushLocalPushAdd(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();

        var a = state.GetLocal(idx1);
        var b = state.GetLocal(idx2);

        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
        {
            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
                state.Push(new DreamValue(a.UnsafeRawLong + b.UnsafeRawLong));
            else
                state.Push(new DreamValue(a.UnsafeRawDouble + b.UnsafeRawDouble));
        }
        else
            state.Push(a + b);
    }

    private static void HandleLocalAddFloat(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        double val = state.ReadDouble();

        var a = state.GetLocal(idx);
        if (a.Type <= DreamValueType.Integer)
            state.Push(new DreamValue(a.UnsafeRawDouble + val));
        else
            state.Push(a + val);
    }

    private static void HandleLocalMulAdd(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        int idx3 = state.ReadInt32();

        var a = state.GetLocal(idx1);
        var b = state.GetLocal(idx2);
        var c = state.GetLocal(idx3);

        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer && c.Type <= DreamValueType.Integer)
        {
            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer && c.Type == DreamValueType.Integer)
                state.Push(new DreamValue(a.UnsafeRawLong * b.UnsafeRawLong + c.UnsafeRawLong));
            else
                state.Push(new DreamValue(a.UnsafeRawDouble * b.UnsafeRawDouble + c.UnsafeRawDouble));
        }
        else
            state.Push(a * b + c);
    }

    private static void HandleLocalPushReturn(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        state.Push(state.GetLocal(idx));
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_Return(ref state.Proc, ref state.PC);
        state.StackPtr = state.Thread._stackPtr;
        state.RefreshSpans();
    }

    private static void HandleLocalCompareEquals(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();

        var a = state.GetLocal(idx1);
        var b = state.GetLocal(idx2);
        state.Push(a == b ? DreamValue.True : DreamValue.False);
    }

    private static void HandleLocalJumpIfFalse(ref InterpreterState state)
    {
        int pcForError = state.PC - 1;
        int idx = state.ReadInt32();
        int address = state.ReadInt32();
        PerformLocalJumpIfFalse(ref state, idx, address, pcForError);
    }

    private static void HandleLocalJumpIfTrue(ref InterpreterState state)
    {
        int pcForError = state.PC - 1;
        int idx = state.ReadInt32();
        int address = state.ReadInt32();
        PerformLocalJumpIfTrue(ref state, idx, address, pcForError);
    }

    private static void HandleLocalCompareNotEquals(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();

        var a = state.GetLocal(idx1);
        var b = state.GetLocal(idx2);
        state.Push(a != b ? DreamValue.True : DreamValue.False);
    }

    private static void HandleLocalIncrement(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        ref var val = ref state.GetLocal(idx);
        val = val + 1;
    }

    private static void HandleLocalDecrement(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        ref var val = ref state.GetLocal(idx);
        val = val - 1;
    }

    private static void HandleLocalPushLocalPushSub(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();

        var a = state.GetLocal(idx1);
        var b = state.GetLocal(idx2);

        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
        {
            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
                state.Push(new DreamValue(a.UnsafeRawLong - b.UnsafeRawLong));
            else
                state.Push(new DreamValue(a.UnsafeRawDouble - b.UnsafeRawDouble));
        }
        else
            state.Push(a - b);
    }

    private static void HandleLocalAddLocalAssign(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        ref var a = ref state.GetLocal(idx1);
        var b = state.GetLocal(idx2);
        a = a + b;
    }

    private static void HandleLocalSubLocalAssign(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        ref var a = ref state.GetLocal(idx1);
        var b = state.GetLocal(idx2);
        a = a - b;
    }

    private static void HandleLocalJumpIfNull(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        int address = state.ReadInt32();
        if (state.GetLocal(idx).IsNull) state.PC = address;
    }

    private static void HandleLocalJumpIfNotNull(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        int address = state.ReadInt32();
        if (!state.GetLocal(idx).IsNull) state.PC = address;
    }

    private static void HandleLocalCompareEqualsJumpIfFalse(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        int address = state.ReadInt32();
        PerformLocalCompareEqualsJumpIfFalse(ref state, idx1, idx2, address);
    }

    private static void HandleLocalCompareNotEqualsJumpIfFalse(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        int address = state.ReadInt32();
        PerformLocalCompareNotEqualsJumpIfFalse(ref state, idx1, idx2, address);
    }

    private static void HandleLocalCompareLessThanJumpIfFalse(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        int address = state.ReadInt32();
        PerformLocalCompareLessThanJumpIfFalse(ref state, idx1, idx2, address);
    }

    private static void HandleLocalCompareGreaterThanJumpIfFalse(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        int address = state.ReadInt32();
        PerformLocalCompareGreaterThanJumpIfFalse(ref state, idx1, idx2, address);
    }

    private static void HandleLocalCompareLessThanOrEqualJumpIfFalse(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        int address = state.ReadInt32();
        PerformLocalCompareLessThanOrEqualJumpIfFalse(ref state, idx1, idx2, address);
    }

    private static void HandleLocalCompareGreaterThanOrEqualJumpIfFalse(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        int address = state.ReadInt32();
        PerformLocalCompareGreaterThanOrEqualJumpIfFalse(ref state, idx1, idx2, address);
    }

    private static void HandleLocalPushDereferenceField(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        int nameId = state.ReadInt32();
        int pcForCache = state.PC - 5;
        PerformLocalPushDereferenceField(ref state, idx, nameId, pcForCache);
    }

    private static void HandleLocalMulLocalAssign(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        ref var a = ref state.GetLocal(idx1);
        var b = state.GetLocal(idx2);
        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
        {
            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
                a = new DreamValue(a.UnsafeRawLong * b.UnsafeRawLong);
            else
                a = new DreamValue(a.UnsafeRawDouble * b.UnsafeRawDouble);
        }
        else a = a * b;
    }

    private static void HandleLocalDivLocalAssign(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        ref var a = ref state.GetLocal(idx1);
        var b = state.GetLocal(idx2);
        double db = b.Type == DreamValueType.Float ? b.UnsafeRawDouble : b.GetValueAsDouble();
        if (db != 0)
        {
            if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
                a = new DreamValue(a.UnsafeRawDouble / db);
            else a = a / b;
        }
        else a = new DreamValue(0.0);
    }

    private static void HandleLocalMulFloatAssign(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        double val = state.ReadDouble();
        ref var a = ref state.GetLocal(idx);
        if (a.Type <= DreamValueType.Integer) a = new DreamValue(a.UnsafeRawDouble * val);
        else a = a * val;
    }

    private static void HandleLocalDivFloatAssign(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        double val = state.ReadDouble();
        ref var a = ref state.GetLocal(idx);
        if (val != 0)
        {
            if (a.Type <= DreamValueType.Integer) a = new DreamValue(a.UnsafeRawDouble / val);
            else a = a / val;
        }
        else a = new DreamValue(0.0);
    }

    private static void HandleLocalMulFloat(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        double val = state.ReadDouble();
        var a = state.GetLocal(idx);
        if (a.Type <= DreamValueType.Integer) state.Push(new DreamValue(a.UnsafeRawDouble * val));
        else state.Push(a * val);
    }

    private static void HandleLocalDivFloat(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        double val = state.ReadDouble();
        var a = state.GetLocal(idx);
        if (val != 0)
        {
            if (a.Type <= DreamValueType.Integer) state.Push(new DreamValue(a.UnsafeRawDouble / val));
            else state.Push(a / val);
        }
        else state.Push(new DreamValue(0.0));
    }

    private static void HandleLocalPushLocalPushMul(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        var a = state.GetLocal(idx1);
        var b = state.GetLocal(idx2);
        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
        {
            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
                state.Push(new DreamValue(a.UnsafeRawLong * b.UnsafeRawLong));
            else
                state.Push(new DreamValue(a.UnsafeRawDouble * b.UnsafeRawDouble));
        }
        else state.Push(a * b);
    }

    private static void HandleLocalPushLocalPushDiv(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        var a = state.GetLocal(idx1);
        var b = state.GetLocal(idx2);
        double db = b.Type == DreamValueType.Float ? b.UnsafeRawDouble : b.GetValueAsDouble();
        if (db != 0)
        {
            if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
                state.Push(new DreamValue(a.UnsafeRawDouble / db));
            else
                state.Push(a / b);
        }
        else state.Push(new DreamValue(0.0));
    }

    private static void HandleLocalAddFloatAssign(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        double val = state.ReadDouble();
        ref var a = ref state.GetLocal(idx);
        if (a.Type <= DreamValueType.Integer) a = new DreamValue(a.UnsafeRawDouble + val);
        else a = a + val;
    }

    private static void HandleLocalCompareLessThan(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        state.Push(state.GetLocal(idx1) < state.GetLocal(idx2) ? DreamValue.True : DreamValue.False);
    }

    private static void HandleLocalCompareGreaterThan(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        state.Push(state.GetLocal(idx1) > state.GetLocal(idx2) ? DreamValue.True : DreamValue.False);
    }

    private static void HandleLocalCompareLessThanOrEqual(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        state.Push(state.GetLocal(idx1) <= state.GetLocal(idx2) ? DreamValue.True : DreamValue.False);
    }

    private static void HandleLocalCompareGreaterThanOrEqual(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        state.Push(state.GetLocal(idx1) >= state.GetLocal(idx2) ? DreamValue.True : DreamValue.False);
    }
}
