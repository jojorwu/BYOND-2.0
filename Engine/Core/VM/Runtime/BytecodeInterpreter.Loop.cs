using Shared.Enums;
using System;
using System.Runtime.CompilerServices;
using Core.VM.Procs;
using Shared;

namespace Core.VM.Runtime;

public unsafe partial class BytecodeInterpreter
{
    public DreamThreadState Run(DreamThread thread, int instructionBudget)
    {
        if (thread.State != DreamThreadState.Running)
            return thread.State;

        _vm?.OnThreadStarted();

        ref var currentFrame = ref thread._callStack[thread._callStackPtr - 1];
        var state = new InterpreterState
        {
            Thread = thread,
            Frame = ref currentFrame,
            Proc = currentFrame.Proc,
            PC = currentFrame.PC,
            Stack = thread._stack.Array,
            Locals = thread._stack.Array.AsSpan(currentFrame.LocalBase, currentFrame.Proc.LocalVariableCount),
            Arguments = thread._stack.Array.AsSpan(currentFrame.ArgumentBase, currentFrame.Proc.Arguments.Length),
            LocalBase = currentFrame.LocalBase,
            ArgumentBase = currentFrame.ArgumentBase,
            StackPtr = thread._stack.Pointer,
            BytecodeArray = currentFrame.Proc.Bytecode,
            BytecodePtr = null, // Initialized in fixed block
            Strings = thread.Context.Strings,
            Globals = thread.Context.Globals,
            Procs = thread.Context.Procs,
            World = thread.Context.World
        };

        int instructionsExecutedThisTick = 0;
        long totalInstructionsExecuted = thread._totalInstructionsExecuted;
        long maxInstructions = thread._maxInstructions;

        while (thread.State == DreamThreadState.Running)
        {
            ReportTelemetry();
            try
            {
                fixed (byte* bytecodePtr = state.BytecodeArray)
                {
                    state.BytecodePtr = bytecodePtr;
                    while (thread.State == DreamThreadState.Running)
                    {
                        // Optimized chunked instruction dispatch:
                        // Execute instructions in batches of 16 to reduce budget checking overhead
                        // while still maintaining responsiveness.
                        int remainingBudget = instructionBudget - instructionsExecutedThisTick;
                        if (remainingBudget <= 0) goto Done;

                        if (totalInstructionsExecuted > maxInstructions)
                        {
                            thread.State = DreamThreadState.Error;
                            goto Done;
                        }

                        int chunk = Math.Min(remainingBudget, 16);
                        int actualExecutedInChunk = 0;
                        for (int i = 0; i < chunk; i++)
                        {
                            if (thread.State != DreamThreadState.Running) break;

                            if (state.PC >= state.BytecodeArray.Length)
                            {
                                // Handle implicit return at end of bytecode
                                thread._stackPtr = state.StackPtr;
                                thread.Push(DreamValue.Null);
                                thread.Opcode_Return(ref state.Proc, ref state.PC);
                                state.Stack = thread._stack.Array;
                                state.StackPtr = thread._stackPtr;

                                if (thread.State == DreamThreadState.Running && thread._callStackPtr > 0)
                                {
                                    state.Frame = ref thread._callStack[thread._callStackPtr - 1];
                                    state.Proc = state.Frame.Proc;
                                    state.PC = state.Frame.PC;
                                    state.Stack = thread._stack.Array;
                                    state.StackPtr = thread._stackPtr;

                                    if (state.Proc.Bytecode != state.BytecodeArray)
                                    {
                                        state.BytecodeArray = state.Proc.Bytecode;
                                        state.RefreshSpans();
                                        goto RePin;
                                    }
                                    state.RefreshSpans();
                                    break; // Break for loop to re-check budget
                                }
                                goto Done;
                            }

                            instructionsExecutedThisTick++;
                            totalInstructionsExecuted++;
                            actualExecutedInChunk++;

                            byte rawOpcode = state.BytecodePtr[state.PC++];
                            var opcode = (Opcode)rawOpcode;

                            // Fast-path switch for hot opcodes to enable better JIT branch prediction
                            switch (opcode)
                            {
                                case Opcode.PushLocal0: state.Push(state.Locals[0]); break;
                                case Opcode.PushLocal1: state.Push(state.Locals[1]); break;
                                case Opcode.PushLocal2: state.Push(state.Locals[2]); break;
                                case Opcode.PushLocal3: state.Push(state.Locals[3]); break;
                                case Opcode.PushLocal4: state.Push(state.Locals[4]); break;
                                case Opcode.PushLocal5: state.Push(state.Locals[5]); break;
                                case Opcode.PushLocal6: state.Push(state.Locals[6]); break;
                                case Opcode.PushLocal7: state.Push(state.Locals[7]); break;
                                case Opcode.PushLocal8: state.Push(state.Locals[8]); break;
                                case Opcode.PushLocal9: state.Push(state.Locals[9]); break;
                                case Opcode.PushLocal10: state.Push(state.Locals[10]); break;
                                case Opcode.PushLocal11: state.Push(state.Locals[11]); break;
                                case Opcode.PushLocal12: state.Push(state.Locals[12]); break;
                                case Opcode.PushLocal13: state.Push(state.Locals[13]); break;
                                case Opcode.PushLocal14: state.Push(state.Locals[14]); break;
                                case Opcode.PushLocal15: state.Push(state.Locals[15]); break;
                                case Opcode.AssignLocal0: state.Locals[0] = state.Stack[state.StackPtr - 1]; break;
                                case Opcode.AssignLocal1: state.Locals[1] = state.Stack[state.StackPtr - 1]; break;
                                case Opcode.AssignLocal2: state.Locals[2] = state.Stack[state.StackPtr - 1]; break;
                                case Opcode.AssignLocal3: state.Locals[3] = state.Stack[state.StackPtr - 1]; break;
                                case Opcode.AssignLocal4: state.Locals[4] = state.Stack[state.StackPtr - 1]; break;
                                case Opcode.AssignLocal5: state.Locals[5] = state.Stack[state.StackPtr - 1]; break;
                                case Opcode.AssignLocal6: state.Locals[6] = state.Stack[state.StackPtr - 1]; break;
                                case Opcode.AssignLocal7: state.Locals[7] = state.Stack[state.StackPtr - 1]; break;
                                case Opcode.AssignLocal8: state.Locals[8] = state.Stack[state.StackPtr - 1]; break;
                                case Opcode.AssignLocal9: state.Locals[9] = state.Stack[state.StackPtr - 1]; break;
                                case Opcode.AssignLocal10: state.Locals[10] = state.Stack[state.StackPtr - 1]; break;
                                case Opcode.AssignLocal11: state.Locals[11] = state.Stack[state.StackPtr - 1]; break;
                                case Opcode.AssignLocal12: state.Locals[12] = state.Stack[state.StackPtr - 1]; break;
                                case Opcode.AssignLocal13: state.Locals[13] = state.Stack[state.StackPtr - 1]; break;
                                case Opcode.AssignLocal14: state.Locals[14] = state.Stack[state.StackPtr - 1]; break;
                                case Opcode.AssignLocal15: state.Locals[15] = state.Stack[state.StackPtr - 1]; break;
                                case Opcode.LocalFieldTransfer:
                                    {
                                        int pcForCache = state.PC;
                                        int srcIdx = *(int*)(state.BytecodePtr + state.PC);
                                        int nameId = *(int*)(state.BytecodePtr + state.PC + 4);
                                        int targetIdx = *(int*)(state.BytecodePtr + state.PC + 8);
                                        state.PC += 12;
                                        PerformLocalFieldTransfer(ref state, srcIdx, nameId, targetIdx, pcForCache);
                                    }
                                    break;
                                case Opcode.GlobalJumpIfFalse:
                                    {
                                        int pcForError = state.PC - 1;
                                        int globalIdx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int address = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        PerformGlobalJumpIfFalse(ref state, globalIdx, address, pcForError);
                                    }
                                    break;
                                case Opcode.PushLocal:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if ((uint)idx >= (uint)state.Locals.Length) throw new ScriptRuntimeException("Local index out of bounds", state.Proc, state.PC - 5, state.Thread);

                                        var val = state.Locals[idx];
                                        var ptr = state.StackPtr;
                                        var stack = state.Stack;
                                        if ((uint)ptr < (uint)stack.Length)
                                        {
                                            stack[ptr] = val;
                                            state.StackPtr = ptr + 1;
                                        }
                                        else
                                        {
                                            state.Thread._stackPtr = ptr;
                                            state.Thread.Push(val);
                                            state.RefreshSpans();
                                            state.StackPtr = state.Thread._stackPtr;
                                        }
                                    }
                                    break;
                                case Opcode.AssignLocal:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if ((uint)idx >= (uint)state.Locals.Length) throw new ScriptRuntimeException("Local index out of bounds", state.Proc, state.PC - 5, state.Thread);
                                        state.Locals[idx] = state.Stack[state.StackPtr - 1];
                                    }
                                    break;
                                case Opcode.BooleanAnd:
                                    {
                                        int jumpAddress = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        PerformBooleanAnd(ref state, jumpAddress);
                                    }
                                    break;
                                case Opcode.BooleanOr:
                                    {
                                        int jumpAddress = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        PerformBooleanOr(ref state, jumpAddress);
                                    }
                                    break;
                                case Opcode.BooleanNot:
                                    PerformBooleanNot(ref state);
                                    break;
                                case Opcode.BitAnd:
                                    {
                                        var b = state.Stack[--state.StackPtr];
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        a = new DreamValue(a.RawLong & b.RawLong);
                                    }
                                    break;
                                case Opcode.BitOr:
                                    {
                                        var b = state.Stack[--state.StackPtr];
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        a = new DreamValue(a.RawLong | b.RawLong);
                                    }
                                    break;
                                case Opcode.BitXor:
                                    {
                                        var b = state.Stack[--state.StackPtr];
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        a = new DreamValue(a.RawLong ^ b.RawLong);
                                    }
                                    break;
                                case Opcode.BitNot:
                                    {
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        a = new DreamValue(~a.RawLong);
                                    }
                                    break;
                                case Opcode.BitShiftLeft:
                                    {
                                        var b = state.Stack[--state.StackPtr];
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        a = new DreamValue(SharedOperations.BitShiftLeft(a.RawLong, b.RawLong));
                                    }
                                    break;
                                case Opcode.BitShiftRight:
                                    {
                                        var b = state.Stack[--state.StackPtr];
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        a = new DreamValue(SharedOperations.BitShiftRight(a.RawLong, b.RawLong));
                                    }
                                    break;
                                case Opcode.Call:
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
                                                    targetProc = state.Thread.Context.AllProcs[procId];
                                                }
                                                break;
                                            case DMReference.Type.SrcProc:
                                                {
                                                    int nameId = *(int*)(state.BytecodePtr + state.PC);
                                                    int pcForCache = state.PC - 1;
                                                    state.PC += 4;
                                                    instance = state.Frame.Instance;
                                                    if (instance != null)
                                                    {
                                                        ref var cache = ref state.Proc._inlineCache[pcForCache];
                                                        if (cache.ObjectType == instance.ObjectType && cache.CachedProc != null)
                                                        {
                                                            targetProc = cache.CachedProc;
                                                        }
                                                        else
                                                        {
                                                            var name = state.Strings[nameId];
                                                            targetProc = instance.ObjectType?.GetProc(name);
                                                            if (targetProc == null) state.Thread.Context.Procs.TryGetValue(name, out targetProc);

                                                            if (targetProc != null)
                                                            {
                                                                cache.ObjectType = instance.ObjectType;
                                                                cache.CachedProc = targetProc;
                                                            }
                                                        }
                                                    }
                                                }
                                                break;
                                            case DMReference.Type.ListIndex:
                                                {
                                                    var index = state.Stack[--state.StackPtr];
                                                    var objValue = state.Stack[--state.StackPtr];
                                                    if (objValue.TryGetValue(out DreamObject? listObj) && listObj is DreamList list)
                                                    {
                                                        DreamValue val = DreamValue.Null;
                                                        if (index.Type <= DreamValueType.Integer)
                                                        {
                                                            int lIdx = (int)index.UnsafeRawDouble - 1;
                                                            if (lIdx >= 0 && lIdx < list.Values.Count) val = list.Values[lIdx];
                                                        }
                                                        else val = list.GetValue(index);
                                                        val.TryGetValue(out targetProc);
                                                    }
                                                }
                                                break;
                                            case DMReference.Type.Local:
                                                {
                                                    int idx = *(int*)(state.BytecodePtr + state.PC);
                                                    state.PC += 4;
                                                    if ((uint)idx >= (uint)state.Locals.Length) throw new ScriptRuntimeException("Local index out of bounds", state.Proc, state.PC - 5, state.Thread);
                                                    state.Locals[idx].TryGetValue(out targetProc);
                                                }
                                                break;
                                            case DMReference.Type.Argument:
                                                {
                                                    int idx = *(int*)(state.BytecodePtr + state.PC);
                                                    state.PC += 4;
                                                    if ((uint)idx >= (uint)state.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", state.Proc, state.PC - 5, state.Thread);
                                                    state.Arguments[idx].TryGetValue(out targetProc);
                                                }
                                                break;
                                            case DMReference.Type.Global:
                                                {
                                                    int idx = *(int*)(state.BytecodePtr + state.PC);
                                                    state.PC += 4;
                                                    if ((uint)idx >= (uint)state.Globals.Count) throw new ScriptRuntimeException($"Invalid global index: {idx}", state.Proc, state.PC - 5, state.Thread);
                                                    state.Globals[idx].TryGetValue(out targetProc);
                                                }
                                                break;
                                            case DMReference.Type.Field:
                                                {
                                                    int nameId = *(int*)(state.BytecodePtr + state.PC);
                                                    int pcForCache = state.PC - 1;
                                                    state.PC += 4;
                                                    var objValue = state.Stack[--state.StackPtr];
                                                    if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
                                                    {
                                                        ref var cache = ref state.Proc._inlineCache[pcForCache];
                                                        if (cache.ObjectType == obj.ObjectType && cache.CachedProc != null)
                                                        {
                                                            targetProc = cache.CachedProc;
                                                        }
                                                        else
                                                        {
                                                            var name = state.Strings[nameId];
                                                            targetProc = obj.ObjectType?.GetProc(name);
                                                            if (targetProc == null)
                                                            {
                                                                var varValue = obj.GetVariable(name);
                                                                if (varValue.TryGetValue(out IDreamProc? procFromVar)) targetProc = procFromVar;
                                                            }
                                                            if (targetProc != null)
                                                            {
                                                                cache.ObjectType = obj.ObjectType;
                                                                cache.CachedProc = targetProc;
                                                            }
                                                        }
                                                        instance = obj;
                                                    }
                                                }
                                                break;
                                            default:
                                                {
                                                    state.PC--;
                                                    var reference = state.ReadReference();
                                                    thread._stackPtr = state.StackPtr;
                                                    var val = thread.GetReferenceValue(reference, ref state.Frame, 0);
                                                    thread.PopCount(thread.GetReferenceStackSize(reference));
                                                    state.StackPtr = thread._stackPtr;
                                                    val.TryGetValue(out targetProc);
                                                }
                                                break;
                                        }

                                        var argType = (DMCallArgumentsType)state.BytecodePtr[state.PC++];
                                        int argStackDelta = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        state.PC += 4; // Skip unused stack delta

                                        if (targetProc == null)
                                        {
                                            state.StackPtr -= argStackDelta;
                                            state.Push(DreamValue.Null);
                                        }
                                        else if (targetProc is NativeProc nativeProc)
                                        {
                                            int argCount = argStackDelta;
                                            int stackBase = state.StackPtr - argStackDelta;
                                            var arguments = state.Stack.Slice(state.StackPtr - argCount, argCount);
                                            state.StackPtr = stackBase;
                                            state.Push(nativeProc.Call(thread, instance, arguments));
                                        }
                                        else
                                        {
                                            thread.SavePC(state.PC);
                                            thread._stackPtr = state.StackPtr;
                                            thread.PerformCall(targetProc, instance, argStackDelta, argStackDelta);
                                            RecordInstructions(actualExecutedInChunk);
                                            goto FrameChanged;
                                        }
                                    }
                                    break;
                                case Opcode.MassConcatenation:
                                    {
                                        int count = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if (count < 0 || count > state.StackPtr) throw new ScriptRuntimeException($"Stack underflow during MassConcatenation: {count}", state.Proc, state.PC - 5, thread);
                                        int baseIdx = state.StackPtr - count;
                                        var result = _formatStringBuilder.Value!;
                                        result.Clear();
                                        for (int j = 0; j < count; j++)
                                        {
                                            state.Stack[baseIdx + j].AppendTo(result);
                                            if (result.Length > 1073741824) throw new ScriptRuntimeException("Maximum string length exceeded during concatenation", state.Proc, state.PC - 5, thread);
                                        }
                                        state.StackPtr -= count;
                                        state.Push(new DreamValue(result.ToString()));
                                    }
                                    break;
                                case Opcode.FormatString:
                                    {
                                        int stringId = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int formatCount = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if ((uint)stringId >= (uint)state.Strings.Count) throw new ScriptRuntimeException($"Invalid string ID: {stringId}", state.Proc, state.PC - 9, thread);
                                        if (formatCount < 0 || formatCount > state.StackPtr) throw new ScriptRuntimeException($"Stack underflow during FormatString: {formatCount}", state.Proc, state.PC - 9, thread);
                                        var formatString = state.Strings[stringId];
                                        int baseIdx = state.StackPtr - formatCount;
                                        var result = _formatStringBuilder.Value!;
                                        result.Clear();
                                        int valueIndex = 0;
                                        for (int j = 0; j < formatString.Length; j++)
                                        {
                                            char c = formatString[j];
                                            if (StringFormatEncoder.Decode(c, out var suffix) && StringFormatEncoder.IsInterpolation(suffix))
                                            {
                                                if (valueIndex < formatCount)
                                                {
                                                    state.Stack[baseIdx + valueIndex++].AppendTo(result);
                                                    if (result.Length > 1073741824) throw new ScriptRuntimeException("Maximum string length exceeded during formatting", state.Proc, state.PC - 9, thread);
                                                }
                                            }
                                            else result.Append(c);
                                        }
                                        state.StackPtr -= formatCount;
                                        state.Push(new DreamValue(result.ToString()));
                                    }
                                    break;
                                case Opcode.DereferenceIndex:
                                    {
                                        var index = state.Stack[--state.StackPtr];
                                        var objValue = state.Stack[--state.StackPtr];
                                        DreamValue val = DreamValue.Null;
                                        if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
                                        {
                                            if (index.Type <= DreamValueType.Integer)
                                            {
                                                int listIdx = (int)index.UnsafeRawDouble - 1;
                                                if (listIdx >= 0 && listIdx < list.Values.Count) val = list.Values[listIdx];
                                            }
                                            else val = list.GetValue(index);
                                        }
                                        else if (objValue.Type == DreamValueType.String && objValue.TryGetValue(out string? str) && str != null)
                                        {
                                            if (index.Type <= DreamValueType.Integer)
                                            {
                                                int listIdx = (int)index.UnsafeRawDouble - 1;
                                                if (listIdx >= 0 && listIdx < str.Length) val = new DreamValue(str[listIdx].ToString());
                                            }
                                        }
                                        state.Push(val);
                                    }
                                    break;
                                case Opcode.IsInList:
                                    {
                                        var listValue = state.Stack[--state.StackPtr];
                                        ref var value = ref state.Stack[state.StackPtr - 1];
                                        bool result = false;
                                        if (listValue.Type == DreamValueType.DreamObject && listValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
                                        {
                                            result = list.Contains(value);
                                        }
                                        value = result ? DreamValue.True : DreamValue.False;
                                    }
                                    break;
                                case Opcode.Length:
                                    {
                                        var value = state.Stack[--state.StackPtr];
                                        if (value.Type == DreamValueType.String && value.TryGetValue(out string? str)) state.Push(new DreamValue(str?.Length ?? 0));
                                        else if (value.Type == DreamValueType.DreamObject && value.TryGetValue(out DreamObject? obj) && obj is DreamList list) state.Push(new DreamValue(list.Values.Count));
                                        else state.Push(new DreamValue(0));
                                    }
                                    break;
                                case Opcode.Prob:
                                    {
                                        var chanceValue = state.Stack[--state.StackPtr];
                                        state.Push(new DreamValue(Random.Shared.NextDouble() * 100 < chanceValue.GetValueAsDouble() ? 1.0 : 0.0));
                                    }
                                    break;
                                case Opcode.Abs:
                                    {
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        a = new DreamValue(Math.Abs(a.GetValueAsDouble()));
                                    }
                                    break;
                                case Opcode.Sqrt:
                                    {
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        a = new DreamValue(Math.Sqrt(a.GetValueAsDouble()));
                                    }
                                    break;
                                case Opcode.Power:
                                    {
                                        var b = state.Stack[--state.StackPtr];
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        double da = a.GetValueAsDouble();
                                        double db = b.GetValueAsDouble();
                                        if (db == 2.0) a = new DreamValue(da * da);
                                        else if (db == 0.5) a = new DreamValue(Math.Sqrt(da));
                                        else if (db == 1.0) { /* a stays a */ }
                                        else if (db == 0.0) a = DreamValue.True;
                                        else a = new DreamValue(Math.Pow(da, db));
                                    }
                                    break;
                                case Opcode.Sin:
                                    {
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        a = new DreamValue(Math.Sin(a.GetValueAsDouble() * (Math.PI / 180.0)));
                                    }
                                    break;
                                case Opcode.Cos:
                                    {
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        a = new DreamValue(Math.Cos(a.GetValueAsDouble() * (Math.PI / 180.0)));
                                    }
                                    break;
                                case Opcode.Tan:
                                    {
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        a = new DreamValue(Math.Tan(a.GetValueAsDouble() * (Math.PI / 180.0)));
                                    }
                                    break;
                                case Opcode.ArcSin:
                                    {
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        a = new DreamValue(Math.Asin(a.GetValueAsDouble()) * (180.0 / Math.PI));
                                    }
                                    break;
                                case Opcode.ArcCos:
                                    {
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        a = new DreamValue(Math.Acos(a.GetValueAsDouble()) * (180.0 / Math.PI));
                                    }
                                    break;
                                case Opcode.ArcTan:
                                    {
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        a = new DreamValue(Math.Atan(a.GetValueAsDouble()) * (180.0 / Math.PI));
                                    }
                                    break;
                                case Opcode.ArcTan2:
                                    {
                                        var y = state.Stack[--state.StackPtr];
                                        ref var x = ref state.Stack[state.StackPtr - 1];
                                        x = new DreamValue(Math.Atan2(y.GetValueAsDouble(), x.GetValueAsDouble()) * (180.0 / Math.PI));
                                    }
                                    break;
                                case Opcode.Log:
                                    {
                                        var b = state.Stack[--state.StackPtr];
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        a = new DreamValue(Math.Log(a.GetValueAsDouble(), b.GetValueAsDouble()));
                                    }
                                    break;
                                case Opcode.LogE:
                                    {
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        a = new DreamValue(Math.Log(a.GetValueAsDouble()));
                                    }
                                    break;
                                case Opcode.IsInRange:
                                    {
                                        var max = state.Stack[--state.StackPtr];
                                        var min = state.Stack[--state.StackPtr];
                                        ref var val = ref state.Stack[state.StackPtr - 1];
                                        if (val.Type <= DreamValueType.Integer && min.Type <= DreamValueType.Integer && max.Type <= DreamValueType.Integer)
                                        {
                                            double dv = val.UnsafeRawDouble;
                                            val = (dv >= min.UnsafeRawDouble && dv <= max.UnsafeRawDouble) ? DreamValue.True : DreamValue.False;
                                        }
                                        else
                                        {
                                            val = (val >= min && val <= max) ? DreamValue.True : DreamValue.False;
                                        }
                                    }
                                    break;
                                case Opcode.IsType:
                                    {
                                        var typeVal = state.Stack[--state.StackPtr];
                                        ref var objVal = ref state.Stack[state.StackPtr - 1];
                                        bool res = false;
                                        if (objVal.Type == DreamValueType.DreamObject && objVal.TryGetValue(out DreamObject? o) && o != null &&
                                            typeVal.Type == DreamValueType.DreamType && typeVal.TryGetValue(out ObjectType? t) && t != null)
                                        {
                                            res = o.ObjectType?.IsSubtypeOf(t) ?? false;
                                        }
                                        objVal = res ? DreamValue.True : DreamValue.False;
                                    }
                                    break;
                                case Opcode.AsType:
                                    {
                                        var typeVal = state.Stack[--state.StackPtr];
                                        ref var objVal = ref state.Stack[state.StackPtr - 1];
                                        bool matches = false;
                                        if (objVal.Type == DreamValueType.DreamObject && objVal.TryGetValue(out DreamObject? o) && o != null &&
                                            typeVal.Type == DreamValueType.DreamType && typeVal.TryGetValue(out ObjectType? t) && t != null)
                                        {
                                            matches = o.ObjectType?.IsSubtypeOf(t) ?? false;
                                        }
                                        if (!matches) objVal = DreamValue.Null;
                                    }
                                    break;
                                case Opcode.PushGlobalVars:
                                    {
                                        thread._stackPtr = state.StackPtr;
                                        thread.Opcode_PushGlobalVars();
                                        state.StackPtr = thread._stackPtr;
                                    }
                                    break;
                                case Opcode.PushArgument:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if ((uint)idx >= (uint)state.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", state.Proc, state.PC - 5, state.Thread);

                                        var val = state.Arguments[idx];
                                        var ptr = state.StackPtr;
                                        var stack = state.Stack;
                                        if ((uint)ptr < (uint)stack.Length)
                                        {
                                            stack[ptr] = val;
                                            state.StackPtr = ptr + 1;
                                        }
                                        else
                                        {
                                            state.Thread._stackPtr = ptr;
                                            state.Thread.Push(val);
                                            state.RefreshSpans();
                                            state.StackPtr = state.Thread._stackPtr;
                                        }
                                    }
                                    break;
                                case Opcode.PushFloat:
                                    state.Push(new DreamValue(*(double*)(state.BytecodePtr + state.PC)));
                                    state.PC += 8;
                                    break;
                                case Opcode.Pop:
                                    state.StackPtr--;
                                    break;
                                case Opcode.PushNull:
                                    state.Push(DreamValue.Null);
                                    break;
                                case Opcode.Add:
                                    PerformAdd(ref state);
                                    break;
                                case Opcode.Jump:
                                    state.PC = *(int*)(state.BytecodePtr + state.PC);
                                    break;
                                case Opcode.JumpIfFalse:
                                    {
                                        int address = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        PerformJumpIfFalse(ref state, address);
                                    }
                                    break;
                                case Opcode.LocalJumpIfFalse:
                                    {
                                        int pcForError = state.PC - 1;
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int address = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        PerformLocalJumpIfFalse(ref state, idx, address, pcForError);
                                    }
                                    break;
                                case Opcode.CompareEquals:
                                    PerformCompareEquals(ref state);
                                    break;
                                case Opcode.CompareNotEquals:
                                    PerformCompareNotEquals(ref state);
                                    break;
                                case Opcode.CompareLessThan:
                                    PerformCompareLessThan(ref state);
                                    break;
                                case Opcode.CompareGreaterThan:
                                    PerformCompareGreaterThan(ref state);
                                    break;
                                case Opcode.CompareLessThanOrEqual:
                                    PerformCompareLessThanOrEqual(ref state);
                                    break;
                                case Opcode.CompareGreaterThanOrEqual:
                                    PerformCompareGreaterThanOrEqual(ref state);
                                    break;
                                case Opcode.CompareEquivalent:
                                    {
                                        var b = state.Stack[--state.StackPtr];
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        if (a.Type == b.Type)
                                        {
                                            if (a.Type <= DreamValueType.Integer)
                                            {
                                                if (a.Type == DreamValueType.Integer) a = (a.UnsafeRawLong == b.UnsafeRawLong) ? DreamValue.True : DreamValue.False;
                                                else a = (Math.Abs(a.UnsafeRawDouble - b.UnsafeRawDouble) < 1e-5) ? DreamValue.True : DreamValue.False;
                                            }
                                            else a = (a.Equals(b)) ? DreamValue.True : DreamValue.False;
                                        }
                                        else a = DreamValue.False;
                                    }
                                    break;
                                case Opcode.CompareNotEquivalent:
                                    {
                                        var b = state.Stack[--state.StackPtr];
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        if (a.Type == b.Type)
                                        {
                                            if (a.Type <= DreamValueType.Integer)
                                            {
                                                if (a.Type == DreamValueType.Integer) a = (a.UnsafeRawLong != b.UnsafeRawLong) ? DreamValue.True : DreamValue.False;
                                                else a = (Math.Abs(a.UnsafeRawDouble - b.UnsafeRawDouble) >= 1e-5) ? DreamValue.True : DreamValue.False;
                                            }
                                            else a = !a.Equals(b) ? DreamValue.True : DreamValue.False;
                                        }
                                        else a = DreamValue.True;
                                    }
                                    break;
                                case Opcode.IsNull:
                                    {
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        a = a.IsNull ? DreamValue.True : DreamValue.False;
                                    }
                                    break;
                                case Opcode.Negate:
                                    {
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        if (a.Type == DreamValueType.Integer) a = new DreamValue(-a.UnsafeRawLong);
                                        else a = new DreamValue(-a.GetValueAsDouble());
                                    }
                                    break;
                                case Opcode.Subtract:
                                    PerformSubtract(ref state);
                                    break;
                                case Opcode.Multiply:
                                    PerformMultiply(ref state);
                                    break;
                                case Opcode.Divide:
                                    PerformDivide(ref state);
                                    break;
                                case Opcode.Modulus:
                                    {
                                        var b = state.Stack[--state.StackPtr];
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
                                        {
                                            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
                                            {
                                                long lb = b.UnsafeRawLong;
                                                a = (lb != 0) ? new DreamValue(a.UnsafeRawLong % lb) : new DreamValue(0L);
                                            }
                                            else
                                            {
                                                double db = b.GetValueAsDouble();
                                                a = (db != 0) ? new DreamValue(a.GetValueAsDouble() % db) : new DreamValue(0.0);
                                            }
                                        }
                                        else a = a % b;
                                    }
                                    break;
                                case Opcode.ModulusModulus:
                                    {
                                        var b = state.Stack[--state.StackPtr];
                                        ref var a = ref state.Stack[state.StackPtr - 1];
                                        a = new DreamValue(SharedOperations.Modulo(a.GetValueAsDouble(), b.GetValueAsDouble()));
                                    }
                                    break;
                                case Opcode.ReturnNull:
                                    state.Push(DreamValue.Null);
                                    thread._stackPtr = state.StackPtr;
                                    thread.Opcode_Return(ref state.Proc, ref state.PC);
                                    state.RefreshSpans();
                                    state.StackPtr = thread._stackPtr;
                                    RecordInstructions(actualExecutedInChunk);
                                    goto FrameChanged;
                                case Opcode.CallStatement:
                                    {
                                        var argType = (DMCallArgumentsType)state.BytecodePtr[state.PC++];
                                        int argStackDelta = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        var instance = state.Frame.Instance;
                                        IDreamProc? parentProc = state.Proc.ParentProc;
                                        if (parentProc == null)
                                        {
                                            if (instance != null && instance.ObjectType != null)
                                            {
                                                ObjectType? current = instance.ObjectType;
                                                while (current != null)
                                                {
                                                    if (current.Procs.ContainsValue(state.Proc))
                                                    {
                                                        parentProc = current.Parent?.GetProc(state.Proc.Name);
                                                        state.Proc.ParentProc = parentProc;
                                                        break;
                                                    }
                                                    current = current.Parent;
                                                }
                                            }
                                        }
                                        if (parentProc != null)
                                        {
                                            thread.SavePC(state.PC);
                                            thread._stackPtr = state.StackPtr;
                                            thread.PerformCall(parentProc, instance, argStackDelta, argStackDelta);
                                            RecordInstructions(actualExecutedInChunk);
                                            goto FrameChanged;
                                        }
                                        else
                                        {
                                            state.StackPtr -= argStackDelta;
                                        state.Push(DreamValue.Null);
                                        }
                                    }
                                    break;
                                case Opcode.DereferenceCall:
                                    {
                                        int nameId = *(int*)(state.BytecodePtr + state.PC);
                                        int pcForCache = state.PC - 1;
                                        state.PC += 4;
                                        var argType = (DMCallArgumentsType)state.BytecodePtr[state.PC++];
                                        int argStackDelta = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        var objValue = state.Stack[state.StackPtr - argStackDelta];
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
                                                thread.SavePC(state.PC);
                                                int argCount = argStackDelta - 1;
                                                int stackBase = state.StackPtr - argStackDelta;
                                                if (argCount > 0) state.Stack.Slice(stackBase + 1, argCount).CopyTo(state.Stack.Slice(stackBase));
                                                state.StackPtr--;
                                                thread._stackPtr = state.StackPtr;
                                                thread.PerformCall(targetProc, obj, argCount, argCount);
                                                RecordInstructions(actualExecutedInChunk);
                                                goto FrameChanged;
                                            }
                                        }
                                        state.StackPtr -= argStackDelta;
                                    state.Push(DreamValue.Null);
                                    }
                                    break;
                                case Opcode.ReturnTrue:
                                    state.Push(DreamValue.True);
                                    thread._stackPtr = state.StackPtr;
                                    thread.Opcode_Return(ref state.Proc, ref state.PC);
                                    state.RefreshSpans();
                                    state.StackPtr = thread._stackPtr;
                                    if (OpcodeMetadataCache.CanModifyCallStack(opcode))
                                    {
                                        RecordInstructions(actualExecutedInChunk);
                                        goto FrameChanged;
                                    }
                                    break;
                                case Opcode.GetBuiltinVar:
                                    {
                                        var varType = (BuiltinVar)state.BytecodePtr[state.PC++];
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
                                        else state.Push(DreamValue.Null);
                                    }
                                    break;
                                case Opcode.SetBuiltinVar:
                                    {
                                        var varType = (BuiltinVar)state.BytecodePtr[state.PC++];
                                        var val = state.Stack[--state.StackPtr];
                                        var instance = state.Frame.Instance as GameObject;
                                        if (instance != null)
                                        {
                                            switch (varType)
                                            {
                                                case BuiltinVar.Icon: val.TryGetValue(out string? s); if (s != null) instance.Icon = s; break;
                                                case BuiltinVar.IconState: val.TryGetValue(out string? s2); if (s2 != null) instance.IconState = s2; break;
                                                case BuiltinVar.Dir: instance.Dir = (int)val.GetValueAsDouble(); break;
                                                case BuiltinVar.Alpha: instance.Alpha = val.GetValueAsDouble(); break;
                                                case BuiltinVar.Color: val.TryGetValue(out string? s3); if (s3 != null) instance.Color = s3; break;
                                                case BuiltinVar.Layer: instance.Layer = val.GetValueAsDouble(); break;
                                                case BuiltinVar.PixelX: instance.PixelX = val.GetValueAsDouble(); break;
                                                case BuiltinVar.PixelY: instance.PixelY = val.GetValueAsDouble(); break;
                                            }
                                        }
                                    }
                                    break;
                                case Opcode.ReturnFalse:
                                    state.Push(DreamValue.False);
                                    thread._stackPtr = state.StackPtr;
                                    thread.Opcode_Return(ref state.Proc, ref state.PC);
                                    state.RefreshSpans();
                                    state.StackPtr = thread._stackPtr;
                                    if (OpcodeMetadataCache.CanModifyCallStack(opcode))
                                    {
                                        RecordInstructions(actualExecutedInChunk);
                                        goto FrameChanged;
                                    }
                                    break;
                                case Opcode.JumpIfNull:
                                    {
                                        var val = state.Stack[--state.StackPtr];
                                        int address = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if (val.Type == DreamValueType.Null) state.PC = address;
                                    }
                                    break;
                                case Opcode.JumpIfNullNoPop:
                                    {
                                        var val = state.Stack[state.StackPtr - 1];
                                        int address = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if (val.Type == DreamValueType.Null) state.PC = address;
                                    }
                                    break;
                                case Opcode.SwitchCase:
                                    {
                                        var caseValue = state.Stack[--state.StackPtr];
                                        var switchValue = state.Stack[state.StackPtr - 1];
                                        int jumpAddress = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if (switchValue == caseValue) state.PC = jumpAddress;
                                    }
                                    break;
                                case Opcode.Assign:
                                    {
                                        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
                                        if (refType == DMReference.Type.Local)
                                        {
                                            int idx = *(int*)(state.BytecodePtr + state.PC);
                                            state.PC += 4;
                                            if ((uint)idx >= (uint)state.Locals.Length) throw new ScriptRuntimeException("Local index out of bounds", state.Proc, state.PC - 5, state.Thread);
                                            state.Locals[idx] = state.Stack[state.StackPtr - 1];
                                        }
                                        else if (refType == DMReference.Type.Argument)
                                        {
                                            int idx = *(int*)(state.BytecodePtr + state.PC);
                                            state.PC += 4;
                                            if ((uint)idx >= (uint)state.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", state.Proc, state.PC - 5, state.Thread);
                                            state.Arguments[idx] = state.Stack[state.StackPtr - 1];
                                        }
                                        else if (refType == DMReference.Type.Global)
                                        {
                                            int idx = *(int*)(state.BytecodePtr + state.PC);
                                            state.PC += 4;
                                            if ((uint)idx >= (uint)state.Globals.Count) throw new ScriptRuntimeException($"Invalid global index: {idx}", state.Proc, state.PC - 5, state.Thread);
                                            state.Globals[idx] = state.Stack[state.StackPtr - 1];
                                        }
                                        else if (refType == DMReference.Type.ListIndex)
                                        {
                                            var value = state.Stack[--state.StackPtr];
                                            var index = state.Stack[--state.StackPtr];
                                            var listValue = state.Stack[--state.StackPtr];
                                            if (listValue.TryGetValue(out DreamObject? listObj) && listObj is DreamList list)
                                            {
                                                if (index.Type <= DreamValueType.Integer)
                                                {
                                                    int listIdx = (int)index.UnsafeRawDouble - 1;
                                                    if (listIdx >= 0 && listIdx < list.Values.Count) list.SetValue(listIdx, value);
                                                    else if (listIdx == list.Values.Count) list.AddValue(value);
                                                }
                                                else list.SetValue(index, value);
                                            }
                                            state.Push(value);
                                        }
                                        else if (refType == DMReference.Type.SrcField)
                                        {
                                            int nameId = *(int*)(state.BytecodePtr + state.PC);
                                            int pcForCache = state.PC - 1;
                                            state.PC += 4;
                                            var instance = state.Frame.Instance;
                                            if (instance != null)
                                            {
                                                var val = state.Stack[state.StackPtr - 1];
                                                ref var cache = ref state.Proc._inlineCache[pcForCache];
                                                if (cache.ObjectType == instance.ObjectType)
                                                {
                                                    instance.SetVariableDirect(cache.VariableIndex, val);
                                                }
                                                else
                                                {
                                                    var name = state.Strings[nameId];
                                                    int varIdx = instance.ObjectType?.GetVariableIndex(name) ?? -1;
                                                    if (varIdx != -1)
                                                    {
                                                        cache.ObjectType = instance.ObjectType;
                                                        cache.VariableIndex = varIdx;
                                                        instance.SetVariableDirect(varIdx, val);
                                                    }
                                                    else instance.SetVariable(name, val);
                                                }
                                            }
                                        }
                                        else if (refType == DMReference.Type.Field)
                                        {
                                            int nameId = *(int*)(state.BytecodePtr + state.PC);
                                            int pcForCache = state.PC - 1;
                                            state.PC += 4;
                                            var val = state.Stack[state.StackPtr - 1]; // Value is at StackPtr-1, Target Object is at StackPtr-2
                                            var targetObjValue = state.Stack[state.StackPtr - 2];
                                            if (targetObjValue.TryGetValue(out DreamObject? obj) && obj != null)
                                            {
                                                ref var cache = ref state.Proc._inlineCache[pcForCache];
                                                if (cache.ObjectType == obj.ObjectType)
                                                {
                                                    obj.SetVariableDirect(cache.VariableIndex, val);
                                                }
                                                else
                                                {
                                                    var name = state.Strings[nameId];
                                                    int varIdx = obj.ObjectType?.GetVariableIndex(name) ?? -1;
                                                    if (varIdx != -1)
                                                    {
                                                        cache.ObjectType = obj.ObjectType;
                                                        cache.VariableIndex = varIdx;
                                                        obj.SetVariableDirect(varIdx, val);
                                                    }
                                                    else obj.SetVariable(name, val);
                                                }
                                            }
                                            state.Stack[state.StackPtr - 2] = val;
                                            state.StackPtr--;
                                        }
                                        else
                                        {
                                            state.PC--;
                                            _dispatchTable[(byte)opcode](ref state);
                                        }
                                    }
                                    break;
                                case Opcode.Enumerate:
                                    {
                                        int enumeratorId = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
                                        if (refType == DMReference.Type.Local)
                                        {
                                            int idx = *(int*)(state.BytecodePtr + state.PC);
                                            state.PC += 4;
                                            int jumpAddress = *(int*)(state.BytecodePtr + state.PC);
                                            state.PC += 4;
                                            if ((uint)idx >= (uint)state.Locals.Length) throw new ScriptRuntimeException("Local index out of bounds", state.Proc, state.PC - 9, state.Thread);
                                            var enumerator = thread.GetEnumerator(enumeratorId);
                                            if (enumerator != null && enumerator.MoveNext()) state.Locals[idx] = enumerator.Current;
                                            else state.PC = jumpAddress;
                                        }
                                        else if (refType == DMReference.Type.Argument)
                                        {
                                            int idx = *(int*)(state.BytecodePtr + state.PC);
                                            state.PC += 4;
                                            int jumpAddress = *(int*)(state.BytecodePtr + state.PC);
                                            state.PC += 4;
                                            if ((uint)idx >= (uint)state.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", state.Proc, state.PC - 9, state.Thread);
                                            var enumerator = thread.GetEnumerator(enumeratorId);
                                            if (enumerator != null && enumerator.MoveNext()) state.Arguments[idx] = enumerator.Current;
                                            else state.PC = jumpAddress;
                                        }
                                        else if (refType == DMReference.Type.Global)
                                        {
                                            int idx = *(int*)(state.BytecodePtr + state.PC);
                                            state.PC += 4;
                                            int jumpAddress = *(int*)(state.BytecodePtr + state.PC);
                                            state.PC += 4;
                                            if ((uint)idx >= (uint)thread.Context.Globals.Count) throw new ScriptRuntimeException($"Invalid global index: {idx}", state.Proc, state.PC - 9, state.Thread);
                                            var enumerator = thread.GetEnumerator(enumeratorId);
                                            if (enumerator != null && enumerator.MoveNext()) thread.Context.Globals[idx] = enumerator.Current;
                                            else state.PC = jumpAddress;
                                        }
                                        else if (refType == DMReference.Type.Field)
                                        {
                                            int nameId = *(int*)(state.BytecodePtr + state.PC);
                                            int pcForCache = state.PC - 1;
                                            state.PC += 4;
                                            int jumpAddress = *(int*)(state.BytecodePtr + state.PC);
                                            state.PC += 4;
                                            var objValue = state.Stack[--state.StackPtr];
                                            var enumerator = thread.GetEnumerator(enumeratorId);
                                            if (enumerator != null && enumerator.MoveNext())
                                            {
                                                var val = enumerator.Current;
                                                if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
                                                {
                                                    ref var cache = ref state.Proc._inlineCache[pcForCache];
                                                    if (cache.ObjectType == obj.ObjectType) obj.SetVariableDirect(cache.VariableIndex, val);
                                                    else
                                                    {
                                                        var name = state.Strings[nameId];
                                                        int varIdx = obj.ObjectType?.GetVariableIndex(name) ?? -1;
                                                        if (varIdx != -1) { cache.ObjectType = obj.ObjectType; cache.VariableIndex = varIdx; obj.SetVariableDirect(varIdx, val); }
                                                        else obj.SetVariable(name, val);
                                                    }
                                                }
                                            }
                                            else state.PC = jumpAddress;
                                        }
                                        else if (refType == DMReference.Type.SrcField)
                                        {
                                            int nameId = *(int*)(state.BytecodePtr + state.PC);
                                            int pcForCache = state.PC - 1;
                                            state.PC += 4;
                                            int jumpAddress = *(int*)(state.BytecodePtr + state.PC);
                                            state.PC += 4;
                                            var enumerator = thread.GetEnumerator(enumeratorId);
                                            if (enumerator != null && enumerator.MoveNext())
                                            {
                                                var val = enumerator.Current;
                                                var instance = state.Frame.Instance;
                                                if (instance != null)
                                                {
                                                    ref var cache = ref state.Proc._inlineCache[pcForCache];
                                                    if (cache.ObjectType == instance.ObjectType) instance.SetVariableDirect(cache.VariableIndex, val);
                                                    else
                                                    {
                                                        var name = state.Strings[nameId];
                                                        int varIdx = instance.ObjectType?.GetVariableIndex(name) ?? -1;
                                                        if (varIdx != -1) { cache.ObjectType = instance.ObjectType; cache.VariableIndex = varIdx; instance.SetVariableDirect(varIdx, val); }
                                                        else instance.SetVariable(name, val);
                                                    }
                                                }
                                            }
                                            else state.PC = jumpAddress;
                                        }
                                        else
                                        {
                                            state.PC--;
                                            _dispatchTable[(byte)opcode](ref state);
                                        }
                                    }
                                    break;
                                case Opcode.PushProc:
                                    {
                                        int procId = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if ((uint)procId >= (uint)thread.Context.AllProcs.Count) throw new ScriptRuntimeException($"Invalid proc ID: {procId}", state.Proc, state.PC - 5, state.Thread);
                                        state.Push(new DreamValue((IDreamProc)thread.Context.AllProcs[procId]));
                                    }
                                    break;
                                case Opcode.PushString:
                                    {
                                        int id = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if ((uint)id >= (uint)state.Strings.Count) throw new ScriptRuntimeException($"Invalid string ID: {id}", state.Proc, state.PC - 5, state.Thread);
                                        state.Push(new DreamValue(state.Strings[id]));
                                    }
                                    break;
                                case Opcode.PushReferenceValue:
                                    {
                                        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
                                        if (refType == DMReference.Type.Local)
                                        {
                                            int idx = *(int*)(state.BytecodePtr + state.PC);
                                            state.PC += 4;
                                            if ((uint)idx >= (uint)state.Locals.Length) throw new ScriptRuntimeException("Local index out of bounds", state.Proc, state.PC - 5, state.Thread);
                                            state.Push(state.Locals[idx]);
                                        }
                                        else if (refType == DMReference.Type.Argument)
                                        {
                                            int idx = *(int*)(state.BytecodePtr + state.PC);
                                            state.PC += 4;
                                            if ((uint)idx >= (uint)state.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", state.Proc, state.PC - 5, state.Thread);
                                            state.Push(state.Arguments[idx]);
                                        }
                                        else if (refType == DMReference.Type.Global)
                                        {
                                            int idx = *(int*)(state.BytecodePtr + state.PC);
                                            state.PC += 4;
                                            if ((uint)idx >= (uint)state.Globals.Count) throw new ScriptRuntimeException($"Invalid global index: {idx}", state.Proc, state.PC - 5, state.Thread);
                                            state.Push(thread.Context.GetGlobal(idx));
                                        }
                                        else if (refType == DMReference.Type.ListIndex)
                                        {
                                            var index = state.Stack[--state.StackPtr];
                                            var listValue = state.Stack[--state.StackPtr];
                                            DreamValue val = DreamValue.Null;
                                            if (listValue.TryGetValue(out DreamObject? listObj) && listObj is DreamList list)
                                            {
                                                if (index.Type <= DreamValueType.Integer)
                                                {
                                                    int listIdx = (int)index.UnsafeRawDouble - 1;
                                                    if (listIdx >= 0 && listIdx < list.Values.Count) val = list.Values[listIdx];
                                                }
                                                else val = list.GetValue(index);
                                            }
                                            state.Push(val);
                                        }
                                        else if (refType == DMReference.Type.SrcField)
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
                                        else if (refType == DMReference.Type.Field)
                                        {
                                            int nameId = *(int*)(state.BytecodePtr + state.PC);
                                            int pcForCache = state.PC - 1;
                                            state.PC += 4;
                                            var objValue = state.Stack[--state.StackPtr];
                                            if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
                                            {
                                                ref var cache = ref state.Proc._inlineCache[pcForCache];
                                                if (cache.ObjectType == obj.ObjectType)
                                                {
                                                    state.Push(obj.GetVariableDirect(cache.VariableIndex));
                                                }
                                                else
                                                {
                                                    var name = state.Strings[nameId];
                                                    int varIdx = obj.ObjectType?.GetVariableIndex(name) ?? -1;
                                                    if (varIdx != -1)
                                                    {
                                                        cache.ObjectType = obj.ObjectType;
                                                        cache.VariableIndex = varIdx;
                                                        state.Push(obj.GetVariableDirect(varIdx));
                                                    }
                                                    else state.Push(obj.GetVariable(name));
                                                }
                                            }
                                            else state.Push(DreamValue.Null);
                                        }
                                        else if (refType == DMReference.Type.Src)
                                        {
                                            state.Push(state.Frame.Instance != null ? new DreamValue(state.Frame.Instance) : DreamValue.Null);
                                        }
                                        else if (refType == DMReference.Type.World)
                                        {
                                            state.Push(thread.Context.World != null ? new DreamValue(thread.Context.World) : DreamValue.Null);
                                        }
                                        else
                                        {
                                            state.PC--;
                                            _dispatchTable[(byte)opcode](ref state);
                                        }
                                    }
                                    break;
                                case Opcode.PopReference:
                                    {
                                        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
                                        if (refType == DMReference.Type.Field) state.PC += 4; // Skip nameId
                                        else if (refType == DMReference.Type.ListIndex) { /* No extra data */ }
                                        else if (refType >= DMReference.Type.Argument && refType <= DMReference.Type.GlobalProc) state.PC += 4;
                                        // Pop reference components from stack
                                        if (refType == DMReference.Type.Field) state.StackPtr--;
                                        else if (refType == DMReference.Type.ListIndex) state.StackPtr -= 2;
                                    }
                                    break;
                                case Opcode.Return:
                                    thread._stackPtr = state.StackPtr;
                                    thread.Opcode_Return(ref state.Proc, ref state.PC);
                                    state.RefreshSpans();
                                    state.StackPtr = thread._stackPtr;
                                    if (OpcodeMetadataCache.CanModifyCallStack(opcode))
                                    {
                                        RecordInstructions(actualExecutedInChunk);
                                        goto FrameChanged;
                                    }
                                    break;
                                case Opcode.GetVariable:
                                    {
                                        int id = *(int*)(state.BytecodePtr + state.PC);
                                        int pcForCache = state.PC - 1;
                                        state.PC += 4;
                                        if ((uint)id >= (uint)state.Strings.Count) throw new ScriptRuntimeException($"Invalid string ID: {id}", state.Proc, state.PC - 5, state.Thread);
                                        var inst = state.Frame.Instance;
                                        if (inst != null)
                                        {
                                            ref var cache = ref state.Proc._inlineCache[pcForCache];
                                            if (cache.ObjectType == inst.ObjectType)
                                            {
                                                state.Push(inst.GetVariableDirect(cache.VariableIndex));
                                            }
                                            else
                                            {
                                                var name = state.Strings[id];
                                                int idx = inst.ObjectType?.GetVariableIndex(name) ?? -1;
                                                if (idx != -1)
                                                {
                                                    cache.ObjectType = inst.ObjectType;
                                                    cache.VariableIndex = idx;
                                                    state.Push(inst.GetVariableDirect(idx));
                                                }
                                                else state.Push(inst.GetVariable(name));
                                            }
                                        }
                                        else state.Push(DreamValue.Null);
                                    }
                                    break;
                                case Opcode.SetVariable:
                                    {
                                        int id = *(int*)(state.BytecodePtr + state.PC);
                                        int pcForCache = state.PC - 1;
                                        state.PC += 4;
                                        if ((uint)id >= (uint)state.Strings.Count) throw new ScriptRuntimeException($"Invalid string ID: {id}", state.Proc, state.PC - 5, state.Thread);
                                        var val = state.Stack[--state.StackPtr];
                                        var inst = state.Frame.Instance;
                                        if (inst != null)
                                        {
                                            ref var cache = ref state.Proc._inlineCache[pcForCache];
                                            if (cache.ObjectType == inst.ObjectType)
                                            {
                                                inst.SetVariableDirect(cache.VariableIndex, val);
                                            }
                                            else
                                            {
                                                var name = state.Strings[id];
                                                int idx = inst.ObjectType?.GetVariableIndex(name) ?? -1;
                                                if (idx != -1)
                                                {
                                                    cache.ObjectType = inst.ObjectType;
                                                    cache.VariableIndex = idx;
                                                    inst.SetVariableDirect(idx, val);
                                                }
                                                else inst.SetVariable(name, val);
                                            }
                                        }
                                    }
                                    break;
                                case Opcode.PushNStrings:
                                    {
                                        int count = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        state.EnsureStack(count);
                                        for (int j = 0; j < count; j++)
                                        {
                                            int id = *(int*)(state.BytecodePtr + state.PC);
                                            state.PC += 4;
                                            state.Push(new DreamValue(state.Strings[id]));
                                        }
                                    }
                                    break;
                                case Opcode.PushNFloats:
                                    {
                                        int count = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        state.EnsureStack(count);
                                        for (int j = 0; j < count; j++)
                                        {
                                            state.Push(new DreamValue(*(double*)(state.BytecodePtr + state.PC)));
                                            state.PC += 8;
                                        }
                                    }
                                    break;
                                case Opcode.PushNResources:
                                    {
                                        int count = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        state.EnsureStack(count);
                                        for (int j = 0; j < count; j++)
                                        {
                                            int id = *(int*)(state.BytecodePtr + state.PC);
                                            state.PC += 4;
                                            state.Push(new DreamValue(new DreamResource("resource", state.Strings[id])));
                                        }
                                    }
                                    break;
                                case Opcode.PushNOfStringFloats:
                                    {
                                        int count = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        state.EnsureStack(count * 2);
                                        for (int j = 0; j < count; j++)
                                        {
                                            int id = *(int*)(state.BytecodePtr + state.PC);
                                            state.PC += 4;
                                            state.Push(new DreamValue(state.Strings[id]));
                                            state.Push(new DreamValue(*(double*)(state.BytecodePtr + state.PC)));
                                            state.PC += 8;
                                        }
                                    }
                                    break;
                                case Opcode.LocalCompareEquals:
                                    {
                                        int idx1 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx2 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        state.Push(state.Locals[idx1] == state.Locals[idx2] ? DreamValue.True : DreamValue.False);
                                    }
                                    break;
                                case Opcode.LocalCompareNotEquals:
                                    {
                                        int idx1 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx2 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        state.Push(state.Locals[idx1] != state.Locals[idx2] ? DreamValue.True : DreamValue.False);
                                    }
                                    break;
                                case Opcode.LocalJumpIfTrue:
                                    {
                                        int pcForError = state.PC - 1;
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int address = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        PerformLocalJumpIfTrue(ref state, idx, address, pcForError);
                                    }
                                    break;
                                case Opcode.LocalPushLocalPushAdd:
                                    {
                                        int idx1 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx2 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        var a = state.Locals[idx1];
                                        var b = state.Locals[idx2];
                                        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
                                        {
                                            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
                                                state.Push(new DreamValue(a.UnsafeRawLong + b.UnsafeRawLong));
                                            else
                                                state.Push(new DreamValue(a.UnsafeRawDouble + b.UnsafeRawDouble));
                                        }
                                        else state.Push(a + b);
                                    }
                                    break;
                                case Opcode.LocalPushLocalPushSub:
                                    {
                                        int idx1 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx2 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        var a = state.Locals[idx1];
                                        var b = state.Locals[idx2];
                                        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
                                        {
                                            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
                                                state.Push(new DreamValue(a.UnsafeRawLong - b.UnsafeRawLong));
                                            else
                                                state.Push(new DreamValue(a.UnsafeRawDouble - b.UnsafeRawDouble));
                                        }
                                        else state.Push(a - b);
                                    }
                                    break;
                                case Opcode.LocalAddFloat:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        double val = *(double*)(state.BytecodePtr + state.PC);
                                        state.PC += 8;
                                        var a = state.Locals[idx];
                                        if (a.Type <= DreamValueType.Integer)
                                            state.Push(new DreamValue(a.GetValueAsDouble() + val));
                                        else
                                            state.Push(a + val);
                                    }
                                    break;
                                case Opcode.LocalMulAdd:
                                    {
                                        int idx1 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx2 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx3 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        var a = state.Locals[idx1];
                                        var b = state.Locals[idx2];
                                        var c = state.Locals[idx3];
                                        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer && c.Type <= DreamValueType.Integer)
                                        {
                                            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer && c.Type == DreamValueType.Integer)
                                                state.Push(new DreamValue(a.UnsafeRawLong * b.UnsafeRawLong + c.UnsafeRawLong));
                                            else
                                                state.Push(new DreamValue(a.UnsafeRawDouble * b.UnsafeRawDouble + c.UnsafeRawDouble));
                                        }
                                        else state.Push(a * b + c);
                                    }
                                    break;
                                case Opcode.LocalIncrement:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        state.Locals[idx] = state.Locals[idx] + 1;
                                    }
                                    break;
                                case Opcode.LocalDecrement:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        state.Locals[idx] = state.Locals[idx] - 1;
                                    }
                                    break;
                                case Opcode.LocalAddLocalAssign:
                                    {
                                        int idx1 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx2 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        state.Locals[idx1] = state.Locals[idx1] + state.Locals[idx2];
                                    }
                                    break;
                                case Opcode.LocalSubLocalAssign:
                                    {
                                        int idx1 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx2 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        state.Locals[idx1] = state.Locals[idx1] - state.Locals[idx2];
                                    }
                                    break;
                                case Opcode.LocalPushReturn:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        state.Push(state.Locals[idx]);
                                        thread._stackPtr = state.StackPtr;
                                        thread.Opcode_Return(ref state.Proc, ref state.PC);
                                        state.RefreshSpans();
                                        state.StackPtr = thread._stackPtr;
                                        RecordInstructions(actualExecutedInChunk);
                                        goto FrameChanged;
                                    }
                                case Opcode.ReturnFloat:
                                    state.Push(new DreamValue(*(double*)(state.BytecodePtr + state.PC)));
                                    state.PC += 8;
                                    thread._stackPtr = state.StackPtr;
                                    thread.Opcode_Return(ref state.Proc, ref state.PC);
                                    state.RefreshSpans();
                                    state.StackPtr = thread._stackPtr;
                                    RecordInstructions(actualExecutedInChunk);
                                    goto FrameChanged;
                                case Opcode.Increment:
                                    {
                                        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
                                        if (refType == DMReference.Type.Local)
                                        {
                                            int idx = *(int*)(state.BytecodePtr + state.PC);
                                            state.PC += 4;
                                            ref var val = ref state.Locals[idx];
                                            var newVal = val + 1;
                                            val = newVal;
                                            state.Push(newVal);
                                        }
                                        else if (refType == DMReference.Type.Argument)
                                        {
                                            int idx = *(int*)(state.BytecodePtr + state.PC);
                                            state.PC += 4;
                                            ref var val = ref state.Arguments[idx];
                                            var newVal = val + 1;
                                            val = newVal;
                                            state.Push(newVal);
                                        }
                                        else
                                        {
                                            state.PC--;
                                            _dispatchTable[(byte)opcode](ref state);
                                        }
                                    }
                                    break;
                                case Opcode.Decrement:
                                    {
                                        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
                                        if (refType == DMReference.Type.Local)
                                        {
                                            int idx = *(int*)(state.BytecodePtr + state.PC);
                                            state.PC += 4;
                                            ref var val = ref state.Locals[idx];
                                            var newVal = val - 1;
                                            val = newVal;
                                            state.Push(newVal);
                                        }
                                        else if (refType == DMReference.Type.Argument)
                                        {
                                            int idx = *(int*)(state.BytecodePtr + state.PC);
                                            state.PC += 4;
                                            ref var val = ref state.Arguments[idx];
                                            var newVal = val - 1;
                                            val = newVal;
                                            state.Push(newVal);
                                        }
                                        else
                                        {
                                            state.PC--;
                                            _dispatchTable[(byte)opcode](ref state);
                                        }
                                    }
                                    break;
                                case Opcode.DereferenceField:
                                    {
                                        int nameId = *(int*)(state.BytecodePtr + state.PC);
                                        int pcForCache = state.PC - 1;
                                        state.PC += 4;
                                        var objValue = state.Stack[--state.StackPtr];
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
                                                int idx = obj.ObjectType?.GetVariableIndex(name) ?? -1;
                                                if (idx != -1)
                                                {
                                                    cache.ObjectType = obj.ObjectType;
                                                    cache.VariableIndex = idx;
                                                    val = obj.GetVariableDirect(idx);
                                                }
                                                else val = obj.GetVariable(name);
                                            }
                                        }
                                        state.Push(val);
                                    }
                                    break;
                                case Opcode.CreateList:
                                    {
                                        int size = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if (size < 0 || state.StackPtr < size) throw new ScriptRuntimeException($"Stack underflow during CreateList: {size}", state.Proc, state.PC - 5, thread);
                                        var list = new DreamList(thread.Context.ListType!, 0);
                                        if (size > 0)
                                        {
                                            list.Populate(state.Stack.Slice(state.StackPtr - size, size));
                                            state.StackPtr -= size;
                                        }
                                        state.Push(new DreamValue(list));
                                    }
                                    break;
                                case Opcode.CreateAssociativeList:
                                    {
                                        int size = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if (size < 0 || state.StackPtr < size * 2) throw new ScriptRuntimeException($"Stack underflow during CreateAssociativeList: {size}", state.Proc, state.PC - 5, thread);
                                        var list = new DreamList(thread.Context.ListType!);
                                        if (size > 0)
                                        {
                                            int baseIdx = state.StackPtr - size * 2;
                                            for (int j = 0; j < size; j++)
                                            {
                                                var k = state.Stack[baseIdx + j * 2];
                                                var v = state.Stack[baseIdx + j * 2 + 1];
                                                list.SetValue(k, v);
                                            }
                                            state.StackPtr -= size * 2;
                                        }
                                        state.Push(new DreamValue(list));
                                    }
                                    break;
                                case Opcode.LocalPushDereferenceField:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int nameId = *(int*)(state.BytecodePtr + state.PC);
                                        int pcForCache = state.PC - 1;
                                        state.PC += 4;

                                        var objValue = state.Locals[idx];
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
                                case Opcode.LocalPushDereferenceIndex:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        var index = state.Pop();
                                        var objValue = state.Locals[idx];
                                        DreamValue val = DreamValue.Null;
                                        if (objValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
                                        {
                                            if (index.Type <= DreamValueType.Integer)
                                            {
                                                int listIdx = (int)index.UnsafeRawDouble - 1;
                                                if (listIdx >= 0 && listIdx < list.Values.Count) val = list.Values[listIdx];
                                            }
                                            else val = list.GetValue(index);
                                        }
                                        state.Push(val);
                                    }
                                    break;
                                case Opcode.LocalPushDereferenceCall:
                                    {
                                        int localIdx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int callNameId = *(int*)(state.BytecodePtr + state.PC);
                                        int callPcForCache = state.PC - 1;
                                        state.PC += 4;
                                        var callArgType = (DMCallArgumentsType)state.BytecodePtr[state.PC++];
                                        int callArgStackDelta = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        var objValue = state.Locals[localIdx];
                                        if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
                                        {
                                            IDreamProc? targetProc;
                                            ref var cache = ref state.Proc._inlineCache[callPcForCache];
                                            if (cache.ObjectType == obj.ObjectType && cache.CachedProc != null)
                                            {
                                                targetProc = cache.CachedProc;
                                            }
                                            else
                                            {
                                                var procName = state.Strings[callNameId];
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
                                                state.Thread._stackPtr = state.StackPtr;
                                                state.Thread.PerformCall(targetProc, obj, callArgStackDelta, callArgStackDelta);
                                                RecordInstructions(actualExecutedInChunk);
                                                goto FrameChanged;
                                            }
                                        }
                                        state.StackPtr -= callArgStackDelta;
                                        state.Push(DreamValue.Null);
                                    }
                                    break;
                                case Opcode.IsTypeDirect:
                                    {
                                        int typeId = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        var value = state.Stack[--state.StackPtr];
                                        bool result = false;
                                        if (value.Type == DreamValueType.DreamObject && value.TryGetValue(out DreamObject? obj) && obj != null)
                                        {
                                            var ot = obj.ObjectType;
                                            if (ot != null)
                                            {
                                                var targetType = state.Thread.Context.ObjectTypeManager?.GetObjectType(typeId);
                                                if (targetType != null) result = ot.IsSubtypeOf(targetType);
                                            }
                                        }
                                        state.Push(result ? DreamValue.True : DreamValue.False);
                                    }
                                    break;
                                case Opcode.LocalJumpIfNull:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int address = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if (state.Locals[idx].IsNull) state.PC = address;
                                    }
                                    break;
                                case Opcode.LocalJumpIfNotNull:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int address = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if (!state.Locals[idx].IsNull) state.PC = address;
                                    }
                                    break;
                                case Opcode.LocalCompareEqualsJumpIfFalse:
                                    {
                                        int idx1 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx2 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int address = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if (state.Locals[idx1] != state.Locals[idx2]) state.PC = address;
                                    }
                                    break;
                                case Opcode.LocalCompareNotEqualsJumpIfFalse:
                                    {
                                        int idx1 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx2 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int address = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if (state.Locals[idx1] == state.Locals[idx2]) state.PC = address;
                                    }
                                    break;
                                case Opcode.LocalCompareLessThanJumpIfFalse:
                                    {
                                        int idx1 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx2 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int address = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if (!(state.Locals[idx1] < state.Locals[idx2])) state.PC = address;
                                    }
                                    break;
                                case Opcode.LocalCompareGreaterThanJumpIfFalse:
                                    {
                                        int idx1 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx2 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int address = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if (!(state.Locals[idx1] > state.Locals[idx2])) state.PC = address;
                                    }
                                    break;
                                case Opcode.LocalCompareLessThanOrEqualJumpIfFalse:
                                    {
                                        int idx1 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx2 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int address = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if (!(state.Locals[idx1] <= state.Locals[idx2])) state.PC = address;
                                    }
                                    break;
                                case Opcode.LocalCompareGreaterThanOrEqualJumpIfFalse:
                                    {
                                        int idx1 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx2 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int address = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if (!(state.Locals[idx1] >= state.Locals[idx2])) state.PC = address;
                                    }
                                    break;
                                case Opcode.LocalMulLocalAssign:
                                    {
                                        int idx1 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx2 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        ref var a = ref state.Locals[idx1];
                                        var b = state.Locals[idx2];
                                        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
                                        {
                                            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
                                                a = new DreamValue(a.UnsafeRawLong * b.UnsafeRawLong);
                                            else
                                                a = new DreamValue(a.UnsafeRawDouble * b.UnsafeRawDouble);
                                        }
                                        else a = a * b;
                                    }
                                    break;
                                case Opcode.LocalDivLocalAssign:
                                    {
                                        int idx1 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx2 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        ref var a = ref state.Locals[idx1];
                                        var b = state.Locals[idx2];
                                        double db = b.Type == DreamValueType.Float ? b.UnsafeRawDouble : b.GetValueAsDouble();
                                        if (db != 0)
                                        {
                                            if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
                                                a = new DreamValue(a.UnsafeRawDouble / db);
                                            else a = a / b;
                                        }
                                        else a = new DreamValue(0.0);
                                    }
                                    break;
                                case Opcode.LocalMulFloatAssign:
                                    {
                                        int localIdx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        double floatVal = *(double*)(state.BytecodePtr + state.PC);
                                        state.PC += 8;
                                        ref var localVal = ref state.Locals[localIdx];
                                        // Arithmetic Fast-path: manual numeric type switching
                                        if (localVal.Type == DreamValueType.Float) localVal = new DreamValue(localVal.UnsafeRawDouble * floatVal);
                                        else if (localVal.Type == DreamValueType.Integer) localVal = new DreamValue(localVal.UnsafeRawLong * (long)floatVal);
                                        else localVal = localVal * floatVal;
                                    }
                                    break;
                                case Opcode.LocalDivFloatAssign:
                                    {
                                        int localIdx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        double floatVal = *(double*)(state.BytecodePtr + state.PC);
                                        state.PC += 8;
                                        ref var localVal = ref state.Locals[localIdx];
                                        // Arithmetic Fast-path: avoid DreamValue division operator overhead
                                        if (floatVal != 0)
                                        {
                                            if (localVal.Type == DreamValueType.Float) localVal = new DreamValue(localVal.UnsafeRawDouble / floatVal);
                                            else if (localVal.Type == DreamValueType.Integer) localVal = new DreamValue(localVal.UnsafeRawLong / (long)floatVal);
                                            else localVal = localVal / floatVal;
                                        }
                                        else localVal = new DreamValue(0.0);
                                    }
                                    break;
                                case Opcode.LocalMulFloat:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        double val = *(double*)(state.BytecodePtr + state.PC);
                                        state.PC += 8;
                                        var a = state.Locals[idx];
                                        if (a.Type <= DreamValueType.Integer) state.Push(new DreamValue(a.UnsafeRawDouble * val));
                                        else state.Push(a * val);
                                    }
                                    break;
                                case Opcode.LocalDivFloat:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        double val = *(double*)(state.BytecodePtr + state.PC);
                                        state.PC += 8;
                                        var a = state.Locals[idx];
                                        if (val != 0)
                                        {
                                            if (a.Type <= DreamValueType.Integer) state.Push(new DreamValue(a.UnsafeRawDouble / val));
                                            else state.Push(a / val);
                                        }
                                        else state.Push(new DreamValue(0.0));
                                    }
                                    break;
                                case Opcode.LocalPushLocalPushMul:
                                    {
                                        int idx1 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx2 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        var a = state.Locals[idx1];
                                        var b = state.Locals[idx2];
                                        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
                                        {
                                            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
                                                state.Push(new DreamValue(a.UnsafeRawLong * b.UnsafeRawLong));
                                            else
                                                state.Push(new DreamValue(a.UnsafeRawDouble * b.UnsafeRawDouble));
                                        }
                                        else state.Push(a * b);
                                    }
                                    break;
                                case Opcode.LocalPushLocalPushDiv:
                                    {
                                        int idx1 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx2 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        var a = state.Locals[idx1];
                                        var b = state.Locals[idx2];
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
                                    break;
                                case Opcode.PopN:
                                    {
                                        int count = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        state.StackPtr -= count;
                                    }
                                    break;
                                case Opcode.LocalAddFloatAssign:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        double val = *(double*)(state.BytecodePtr + state.PC);
                                        state.PC += 8;
                                        ref var a = ref state.Locals[idx];
                                        // Arithmetic Fast-path: direct numeric type switching to bypass DreamValue overhead
                                        if (a.Type == DreamValueType.Float) a = new DreamValue(a.UnsafeRawDouble + val);
                                        else if (a.Type == DreamValueType.Integer) a = new DreamValue(a.UnsafeRawLong + (long)val);
                                        else a = a + val;
                                    }
                                    break;
                                case Opcode.LocalCompareLessThan:
                                    {
                                        int idx1 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx2 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        state.Push(state.Locals[idx1] < state.Locals[idx2] ? DreamValue.True : DreamValue.False);
                                    }
                                    break;
                                case Opcode.LocalCompareGreaterThan:
                                    {
                                        int idx1 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx2 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        state.Push(state.Locals[idx1] > state.Locals[idx2] ? DreamValue.True : DreamValue.False);
                                    }
                                    break;
                                case Opcode.LocalCompareLessThanOrEqual:
                                    {
                                        int idx1 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx2 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        state.Push(state.Locals[idx1] <= state.Locals[idx2] ? DreamValue.True : DreamValue.False);
                                    }
                                    break;
                                case Opcode.LocalCompareGreaterThanOrEqual:
                                    {
                                        int idx1 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int idx2 = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        state.Push(state.Locals[idx1] >= state.Locals[idx2] ? DreamValue.True : DreamValue.False);
                                    }
                                    break;
                                case Opcode.LocalCompareLessThanFloatJumpIfFalse:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        double val = *(double*)(state.BytecodePtr + state.PC);
                                        state.PC += 8;
                                        int address = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if (!(state.Locals[idx] < val)) state.PC = address;
                                    }
                                    break;
                                case Opcode.LocalCompareGreaterThanFloatJumpIfFalse:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        double val = *(double*)(state.BytecodePtr + state.PC);
                                        state.PC += 8;
                                        int address = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if (!(state.Locals[idx] > val)) state.PC = address;
                                    }
                                    break;
                                case Opcode.LocalCompareLessThanOrEqualFloatJumpIfFalse:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        double val = *(double*)(state.BytecodePtr + state.PC);
                                        state.PC += 8;
                                        int address = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if (!(state.Locals[idx] <= val)) state.PC = address;
                                    }
                                    break;
                                case Opcode.LocalCompareGreaterThanOrEqualFloatJumpIfFalse:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        double val = *(double*)(state.BytecodePtr + state.PC);
                                        state.PC += 8;
                                        int address = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        if (!(state.Locals[idx] >= val)) state.PC = address;
                                    }
                                    break;
                                case Opcode.LocalJumpIfFieldFalse:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int nameId = *(int*)(state.BytecodePtr + state.PC);
                                        int pcForCache = state.PC - 1;
                                        state.PC += 4;
                                        int address = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        var objValue = state.Locals[idx];
                                        if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
                                        {
                                            ref var cache = ref state.Proc._inlineCache[pcForCache];
                                            DreamValue val;
                                            if (cache.ObjectType == obj.ObjectType) val = obj.GetVariableDirect(cache.VariableIndex);
                                            else
                                            {
                                                var name = state.Strings[nameId];
                                                int varIdx = obj.ObjectType?.GetVariableIndex(name) ?? -1;
                                                if (varIdx != -1) { cache.ObjectType = obj.ObjectType; cache.VariableIndex = varIdx; val = obj.GetVariableDirect(varIdx); }
                                                else val = obj.GetVariable(name);
                                            }
                                            if (val.IsFalse()) state.PC = address;
                                        }
                                        else throw new ScriptRuntimeException($"Field access on null object: {state.Strings[nameId]}", state.Proc, pcForCache, thread);
                                    }
                                    break;
                                case Opcode.LocalJumpIfFieldTrue:
                                    {
                                        int idx = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        int nameId = *(int*)(state.BytecodePtr + state.PC);
                                        int pcForCache = state.PC - 1;
                                        state.PC += 4;
                                        int address = *(int*)(state.BytecodePtr + state.PC);
                                        state.PC += 4;
                                        var objValue = state.Locals[idx];
                                        if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
                                        {
                                            ref var cache = ref state.Proc._inlineCache[pcForCache];
                                            DreamValue val;
                                            if (cache.ObjectType == obj.ObjectType) val = obj.GetVariableDirect(cache.VariableIndex);
                                            else
                                            {
                                                var name = state.Strings[nameId];
                                                int varIdx = obj.ObjectType?.GetVariableIndex(name) ?? -1;
                                                if (varIdx != -1) { cache.ObjectType = obj.ObjectType; cache.VariableIndex = varIdx; val = obj.GetVariableDirect(varIdx); }
                                                else val = obj.GetVariable(name);
                                            }
                                            if (!val.IsFalse()) state.PC = address;
                                        }
                                        else throw new ScriptRuntimeException($"Field access on null object: {state.Strings[nameId]}", state.Proc, pcForCache, thread);
                                    }
                                    break;
                                default:
                                    _dispatchTable[(byte)opcode](ref state);
                                    if (OpcodeMetadataCache.CanModifyCallStack(opcode))
                                    {
                                        RecordInstructions(actualExecutedInChunk);
                                        goto FrameChanged;
                                    }
                                    break;
                            }

                            continue;

                        FrameChanged:
                            if (thread.State == DreamThreadState.Running && thread._callStackPtr > 0)
                            {
                                state.Frame = ref thread._callStack[thread._callStackPtr - 1];
                                state.Proc = state.Frame.Proc;
                                state.PC = state.Frame.PC;
                                state.Stack = thread._stack.Array;
                                state.StackPtr = thread._stack.Pointer;
                                if (state.Proc.Bytecode != state.BytecodeArray)
                                {
                                    state.BytecodeArray = state.Proc.Bytecode;
                                    state.RefreshSpans();
                                    goto RePin;
                                }
                                state.RefreshSpans();
                            }
                            if (thread.State != DreamThreadState.Running) break;
                        }
                        RecordInstructions(actualExecutedInChunk);
                    }
                RePin:;
                }
            }
            catch (Exception e)
            {
                _vm?.OnExceptionThrown();
                thread._stackPtr = state.StackPtr;
                var runtimeException = e as ScriptRuntimeException ?? new ScriptRuntimeException("Unexpected internal error", state.Proc, state.PC, thread, e);

                if (!thread.HandleException(runtimeException))
                    break;

                if (thread._callStackPtr > 0)
                {
                    state.Frame = ref thread._callStack[thread._callStackPtr - 1];
                    state.Proc = state.Frame.Proc;
                    state.PC = state.Frame.PC;
                    state.BytecodeArray = state.Proc.Bytecode;
                    state.Stack = thread._stack.Array;
                    state.StackPtr = thread._stackPtr;
                    state.LocalBase = state.Frame.LocalBase;
                    state.ArgumentBase = state.Frame.ArgumentBase;
                }
            }
        }

    Done:
        thread._totalInstructionsExecuted = totalInstructionsExecuted;
        if (thread._callStackPtr > 0)
        {
            thread.SavePC(state.PC);
        }

        thread._stack.Pointer = state.StackPtr;
        if (thread.State == DreamThreadState.Finished || thread.State == DreamThreadState.Error)
        {
            _vm?.OnThreadFinished(thread);
        }
        return thread.State;
    }
}
