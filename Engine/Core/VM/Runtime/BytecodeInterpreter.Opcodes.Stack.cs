using Shared.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Core.VM.Procs;
using Shared;

namespace Core.VM.Runtime;

public unsafe partial class BytecodeInterpreter
{
    private static void HandlePushString(ref InterpreterState state)
    {
        var stringId = state.ReadInt32();
        if (stringId < 0 || stringId >= state.Thread.Context.Strings.Count)
            throw new ScriptRuntimeException($"Invalid string ID: {stringId}", state.Proc, state.PC, state.Thread);
        state.Push(new DreamValue(state.Thread.Context.Strings[stringId]));
    }

    private static void HandlePushFloat(ref InterpreterState state)
    {
        state.Push(new DreamValue(state.ReadDouble()));
    }

    private static void HandlePushNull(ref InterpreterState state)
    {
        state.Push(DreamValue.Null);
    }

    private static void HandlePop(ref InterpreterState state)
    {
        state.StackPtr--;
    }

    private static void HandlePushProc(ref InterpreterState state)
    {
        var procId = state.ReadInt32();
        DreamValue val;
        if (procId >= 0 && procId < state.Thread.Context.AllProcs.Count)
            val = new DreamValue((IDreamProc)state.Thread.Context.AllProcs[procId]);
        else
            val = DreamValue.Null;
        state.Push(val);
    }

    private static void HandlePopReference(ref InterpreterState state)
    {
        var reference = state.ReadReference();
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandlePushType(ref InterpreterState state)
    {
        var typeId = state.ReadInt32();
        var type = state.Thread.Context.ObjectTypeManager?.GetObjectType(typeId);
        state.Push(type != null ? new DreamValue(type) : DreamValue.Null);
    }

    private static void HandlePushNFloats(ref InterpreterState state)
    {
        var count = state.ReadInt32();
        for (int i = 0; i < count; i++) state.Push(new DreamValue(state.ReadDouble()));
    }

    private static void HandlePushStringFloat(ref InterpreterState state)
    {
        var stringId = state.ReadInt32();
        var value = state.ReadDouble();
        state.Push(new DreamValue(state.Thread.Context.Strings[stringId]));
        state.Push(new DreamValue(value));
    }

    private static void HandlePushResource(ref InterpreterState state)
    {
        var pathId = state.ReadInt32();
        state.Push(new DreamValue(new DreamResource("resource", state.Thread.Context.Strings[pathId])));
    }

    private static void HandleNPushFloatAssign(ref InterpreterState state)
    {
        int n = state.ReadInt32();
        double value = state.ReadDouble();
        var dv = new DreamValue(value);

        for (int i = 0; i < n; i++)
        {
            var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
            if (refType == DMReference.Type.Local)
            {
                int idx = *(int*)(state.BytecodePtr + state.PC);
                state.PC += 4;
                state.Stack[state.LocalBase + idx] = dv;
            }
            else if (refType == DMReference.Type.Argument)
            {
                int idx = *(int*)(state.BytecodePtr + state.PC);
                state.PC += 4;
                state.Stack[state.ArgumentBase + idx] = dv;
            }
            else if (refType == DMReference.Type.Global)
            {
                int globalIdx = *(int*)(state.BytecodePtr + state.PC);
                state.PC += 4;
                state.Thread.Context.SetGlobal(globalIdx, dv);
            }
            else
            {
                state.PC--;
                var reference = state.ReadReference();
                state.Thread._stackPtr = state.StackPtr;
                state.Thread.SetReferenceValue(reference, ref state.Frame, dv, 0);
                state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                state.StackPtr = state.Thread._stackPtr;
            }
        }
        state.Push(dv);
    }

    private static void HandlePushFloatAssign(ref InterpreterState state)
    {
        var value = state.ReadDouble();
        var reference = state.ReadReference();
        var dv = new DreamValue(value);
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.SetReferenceValue(reference, ref state.Frame, dv, 0);
        state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
        state.Thread.Push(dv);
        state.Stack = state.Thread._stack.Array;
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandlePopN(ref InterpreterState state)
    {
        int count = state.ReadInt32();
        state.StackPtr -= count;
    }

}
