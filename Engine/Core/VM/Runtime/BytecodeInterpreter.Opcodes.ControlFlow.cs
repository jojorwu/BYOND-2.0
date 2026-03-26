using Shared.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Core.VM.Procs;
using Shared;

namespace Core.VM.Runtime;

public unsafe partial class BytecodeInterpreter
{
    private static void HandleBooleanNot(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during BooleanNot", state.Proc, state.PC, state.Thread);
        PerformBooleanNot(ref state);
    }

    private static void HandleCall(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        IDreamProc? targetProc = null;
        DreamObject? instance = null;

        switch (refType)
        {
            case DMReference.Type.GlobalProc:
                {
                    int procId = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    if (procId >= 0 && procId < state.Thread.Context.AllProcs.Count)
                        targetProc = state.Thread.Context.AllProcs[procId];
                }
                break;
            case DMReference.Type.SrcProc:
                {
                    int nameId = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    instance = state.Frame.Instance;
                    if (instance != null)
                    {
                        var name = state.Thread.Context.Strings[nameId];
                        targetProc = instance.ObjectType?.GetProc(name);
                        if (targetProc == null) state.Thread.Context.Procs.TryGetValue(name, out targetProc);
                    }
                }
                break;
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Stack[state.LocalBase + idx];
                    val.TryGetValue(out targetProc);
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Stack[state.ArgumentBase + idx];
                    val.TryGetValue(out targetProc);
                }
                break;
            case DMReference.Type.Global:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Thread.Context.GetGlobal(idx);
                    val.TryGetValue(out targetProc);
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
                    val.TryGetValue(out targetProc);
                }
                break;
        }

        var argType = (DMCallArgumentsType)state.BytecodePtr[state.PC++];
        var argStackDelta = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;
        var unusedStackDelta = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;

        if (targetProc == null)
        {
            state.StackPtr -= argStackDelta;
            state.Push(DreamValue.Null);
            return;
        }

        if (targetProc is NativeProc nativeProc)
        {
            var argCount = argStackDelta;
            var stackBase = state.StackPtr - argStackDelta;
            var arguments = state.Stack.Slice(state.StackPtr - argCount, argCount);

            state.StackPtr = stackBase;
            try
            {
                var result = nativeProc.Call(state.Thread, instance, arguments);
                state.Push(result);
            }
            catch (Exception e)
            {
                throw new ScriptRuntimeException(e.Message, state.Proc, state.PC, state.Thread, e);
            }
            return;
        }

        state.Thread.SavePC(state.PC);
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.PerformCall(targetProc, instance, argStackDelta, argStackDelta);
    }

    private static void HandleCallStatement(ref InterpreterState state)
    {
        var argType = (DMCallArgumentsType)state.BytecodePtr[state.PC++];
        int argStackDelta = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;

        if (argStackDelta < 0 || state.StackPtr < argStackDelta)
            throw new ScriptRuntimeException($"Invalid argument stack delta for ..() call: {argStackDelta}", state.Proc, state.PC, state.Thread);

        var instance = state.Frame.Instance;
        IDreamProc? parentProc = null;

        if (instance != null && instance.ObjectType != null)
        {
            ObjectType? definingType = null;
            ObjectType? current = instance.ObjectType;
            while (current != null)
            {
                if (current.Procs.ContainsValue(state.Proc))
                {
                    definingType = current;
                    break;
                }
                current = current.Parent;
            }

            if (definingType != null)
            {
                parentProc = definingType.Parent?.GetProc(state.Proc.Name);
            }
        }

        if (parentProc != null)
        {
            state.Thread.SavePC(state.PC);
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.PerformCall(parentProc, instance, argStackDelta, argStackDelta);
        }
        else
        {
            state.StackPtr -= argStackDelta;
            state.Push(DreamValue.Null);
        }
    }

    private static void HandleJump(ref InterpreterState state)
    {
        state.PC = state.ReadInt32();
    }

    private static void HandleJumpIfFalse(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during JumpIfFalse", state.Proc, state.PC, state.Thread);
        int address = state.ReadInt32();
        PerformJumpIfFalse(ref state, address);
    }

    private static void HandleJumpIfTrueReference(ref InterpreterState state)
    {
        PerformJumpIfTrueReference(ref state);
    }

    private static void HandleJumpIfFalseReference(ref InterpreterState state)
    {
        PerformJumpIfFalseReference(ref state);
    }

    private static void HandleReturn(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_Return(ref state.Proc, ref state.PC);
        state.RefreshSpans();
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleJumpIfNull(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during JumpIfNull", state.Proc, state.PC, state.Thread);
        var val = state.Stack[--state.StackPtr];
        var address = state.ReadInt32();
        if (val.Type == DreamValueType.Null) state.PC = address;
    }

    private static void HandleJumpIfNullNoPop(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during JumpIfNullNoPop", state.Proc, state.PC, state.Thread);
        var val = state.Stack[state.StackPtr - 1];
        var address = state.ReadInt32();
        if (val.Type == DreamValueType.Null) state.PC = address;
    }

    private static void HandleSwitchCase(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during SwitchCase", state.Proc, state.PC, state.Thread);
        var caseValue = state.Stack[--state.StackPtr];
        var switchValue = state.Stack[state.StackPtr - 1];
        var jumpAddress = state.ReadInt32();
        if (switchValue == caseValue) state.PC = jumpAddress;
    }

    private static void HandleSwitchCaseRange(ref InterpreterState state)
    {
        if (state.StackPtr < 3) throw new ScriptRuntimeException("Stack underflow during SwitchCaseRange", state.Proc, state.PC, state.Thread);
        var max = state.Stack[--state.StackPtr];
        var min = state.Stack[--state.StackPtr];
        var switchValue = state.Stack[state.StackPtr - 1];
        var jumpAddress = state.ReadInt32();
        if (switchValue >= min && switchValue <= max) state.PC = jumpAddress;
    }

    private static void HandleBooleanAnd(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during BooleanAnd", state.Proc, state.PC, state.Thread);
        int jumpAddress = state.ReadInt32();
        PerformBooleanAnd(ref state, jumpAddress);
    }

    private static void HandleBooleanOr(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during BooleanOr", state.Proc, state.PC, state.Thread);
        int jumpAddress = state.ReadInt32();
        PerformBooleanOr(ref state, jumpAddress);
    }

    private static void HandleThrow(ref InterpreterState state)
    {
        var value = state.Stack[--state.StackPtr];
        var e = new ScriptRuntimeException(value.ToString(), state.Proc, state.PC, thread: state.Thread) { ThrownValue = value };
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.HandleException(e);
        state.Stack = state.Thread._stack.Array;
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleTry(ref InterpreterState state)
    {
        var catchAddress = state.ReadInt32();
        var catchRef = state.ReadReference();
        state.Thread.PushTryBlock(new TryBlock { CatchAddress = catchAddress, CallStackDepth = state.Thread.CallStackCount, StackPointer = state.StackPtr, CatchReference = catchRef });
    }

    private static void HandleTryNoValue(ref InterpreterState state)
    {
        var catchAddress = state.ReadInt32();
        state.Thread.PushTryBlock(new TryBlock { CatchAddress = catchAddress, CallStackDepth = state.Thread.CallStackCount, StackPointer = state.StackPtr, CatchReference = null });
    }

    private static void HandleEndTry(ref InterpreterState state)
    {
        state.Thread.PopTryBlock();
    }

    private static void HandleSwitchOnFloat(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during SwitchOnFloat", state.Proc, state.PC, state.Thread);
        var value = state.ReadDouble();
        var jumpAddress = state.ReadInt32();
        var switchValue = state.Stack[state.StackPtr - 1];
        if (switchValue.Type <= DreamValueType.Integer && switchValue.UnsafeRawDouble == value) state.PC = jumpAddress;
    }

    private static void HandleSwitchOnString(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during SwitchOnString", state.Proc, state.PC, state.Thread);
        var stringId = state.ReadInt32();
        var jumpAddress = state.ReadInt32();
        var switchValue = state.Stack[state.StackPtr - 1];
        if (switchValue.Type == DreamValueType.String && switchValue.TryGetValue(out string? s) && s == state.Thread.Context.Strings[stringId]) state.PC = jumpAddress;
    }

    private static void HandleJumpIfReferenceFalse(ref InterpreterState state)
    {
        PerformJumpIfFalseReference(ref state);
    }

    private static void HandleReturnFloat(ref InterpreterState state)
    {
        state.Push(new DreamValue(state.ReadDouble()));
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_Return(ref state.Proc, ref state.PC);
        state.RefreshSpans();
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleReturnReferenceValue(ref InterpreterState state)
    {
        var reference = state.ReadReference();
        state.Thread._stackPtr = state.StackPtr;
        var val = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
        state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
        state.Thread.Push(val);
        state.Thread.Opcode_Return(ref state.Proc, ref state.PC);
        state.RefreshSpans();
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleReturnNull(ref InterpreterState state)
    {
        state.Push(DreamValue.Null);
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_Return(ref state.Proc, ref state.PC);
        state.RefreshSpans();
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleReturnTrue(ref InterpreterState state)
    {
        state.Push(DreamValue.True);
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_Return(ref state.Proc, ref state.PC);
        state.RefreshSpans();
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleReturnFalse(ref InterpreterState state)
    {
        state.Push(DreamValue.False);
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_Return(ref state.Proc, ref state.PC);
        state.RefreshSpans();
        state.StackPtr = state.Thread._stackPtr;
    }

}
