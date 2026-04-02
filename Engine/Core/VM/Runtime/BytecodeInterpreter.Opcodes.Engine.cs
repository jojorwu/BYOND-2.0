using Shared.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Core.VM.Procs;
using Shared;

namespace Core.VM.Runtime;

public unsafe partial class BytecodeInterpreter
{
    private static void HandleOutput(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Output", state.Proc, state.PC, state.Thread);
        var message = state.Pop();
        var target = state.Pop();
        if (!message.IsNull) Console.WriteLine(message.ToString());
    }

    private static void HandleOutputReference(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_OutputReference(state.Proc, ref state.Frame, ref state.PC);
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleCreateList(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_CreateList(state.Proc, ref state.PC);
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleCreateAssociativeList(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_CreateAssociativeList(state.Proc, ref state.PC);
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleCreateStrictAssociativeList(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_CreateStrictAssociativeList(state.Proc, ref state.PC);
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleIsInList(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during IsInList", state.Proc, state.PC, state.Thread);
        var listValue = state.Pop();
        ref var value = ref state.Peek();

        bool res = false;
        if (listValue.Type == DreamValueType.DreamObject && listValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
        {
            res = list.Contains(value);
        }
        value = res ? DreamValue.True : DreamValue.False;
    }

    private static void HandleInput(ref InterpreterState state)
    {
        var ref1 = state.ReadReference();
        var ref2 = state.ReadReference();
        state.StackPtr -= 4;
        state.Push(DreamValue.Null);
    }

    private static void HandlePickUnweighted(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_PickUnweighted(state.Proc, ref state.PC);
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandlePickWeighted(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_PickWeighted(state.Proc, ref state.PC);
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleDereferenceField(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during DereferenceField", state.Proc, state.PC, state.Thread);
        int pcForCache = state.PC - 1;
        var nameId = state.ReadInt32();
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

    private static void HandleDereferenceIndex(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during DereferenceIndex", state.Proc, state.PC, state.Thread);
        var index = state.Pop();
        var objValue = state.Pop();
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
        else if (objValue.Type == DreamValueType.String && objValue.TryGetValue(out string? str) && str != null)
        {
            if (index.Type <= DreamValueType.Integer)
            {
                int i = (int)index.UnsafeRawDouble - 1;
                if (i >= 0 && i < str.Length) val = new DreamValue(str[i].ToString());
            }
        }
        state.Push(val);
    }

    private static void HandleDereferenceCall(ref InterpreterState state)
    {
        int pcForCache = state.PC - 1;
        int nameId = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;
        var argType = (DMCallArgumentsType)state.BytecodePtr[state.PC++];
        int argStackDelta = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;

        if (argStackDelta < 1 || state.StackPtr < argStackDelta)
            throw new ScriptRuntimeException($"Invalid argument stack delta for dereference call: {argStackDelta}", state.Proc, state.PC, state.Thread);

        var objValue = state.GetStack(state.StackPtr - argStackDelta);
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
                int argCount = argStackDelta - 1;
                int stackBase = state.StackPtr - argStackDelta;
                // Shift arguments to overwrite the object reference on the stack
                if (argCount > 0)
                {
                    state.StackSpan.Slice(stackBase + 1, argCount).CopyTo(state.StackSpan.Slice(stackBase));
                }
                state.StackPtr--;
                state.Thread._stackPtr = state.StackPtr;
                state.Thread.PerformCall(targetProc, obj, argCount, argCount);
                return;
            }
        }
        state.StackPtr -= argStackDelta;
        state.Push(DreamValue.Null);
    }

    private static void HandleInitial(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Initial", state.Proc, state.PC, state.Thread);
        var key = state.Pop();
        var objValue = state.Pop();
        DreamValue result = DreamValue.Null;

        ObjectType? type = null;
        if (objValue.Type == DreamValueType.DreamType)
        {
            objValue.TryGetValue(out type);
        }
        else if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
        {
            type = obj.ObjectType;
        }

        if (type != null && key.TryGetValue(out string? varName) && varName != null)
        {
            int index = type.GetVariableIndex(varName);
            if (index != -1 && index < type.FlattenedDefaultValues.Count)
                result = type.FlattenedDefaultValues[index];
        }
        state.Push(result);
    }

    private static void HandleCreateListEnumerator(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_CreateListEnumerator(state.Proc, ref state.PC);
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleEnumerate(ref InterpreterState state)
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
            var enumerator = state.Thread.GetEnumerator(enumeratorId);
            if (enumerator != null && enumerator.MoveNext())
                state.GetLocal(idx) = enumerator.Current;
            else
                state.PC = jumpAddress;
        }
        else if (refType == DMReference.Type.Argument)
        {
            int idx = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;
            int jumpAddress = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;
            var enumerator = state.Thread.GetEnumerator(enumeratorId);
            if (enumerator != null && enumerator.MoveNext())
                state.GetArgument(idx) = enumerator.Current;
            else
                state.PC = jumpAddress;
        }
        else
        {
            state.PC--;
            var reference = state.ReadReference();
            int jumpAddress = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;
            var enumerator = state.Thread.GetEnumerator(enumeratorId);
            if (enumerator != null && enumerator.MoveNext())
            {
                state.Thread.SetReferenceValue(reference, ref state.Frame, enumerator.Current, 0);
                state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            }
            else state.PC = jumpAddress;
        }
    }

    private static void HandleEnumerateAssoc(ref InterpreterState state)
    {
        int enumeratorId = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;
        var refType1 = (DMReference.Type)state.BytecodePtr[state.PC];
        if (refType1 == DMReference.Type.Local && (DMReference.Type)state.BytecodePtr[state.PC + 5] == DMReference.Type.Local)
        {
            state.PC++;
            int idx1 = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;
            state.PC++;
            int idx2 = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;
            int jumpAddress = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;

            var enumerator = state.Thread.GetEnumerator(enumeratorId);
            if (enumerator != null && enumerator.MoveNext())
            {
                var key = enumerator.Current;
                state.GetLocal(idx2) = key;
                var list = state.Thread.GetEnumeratorList(enumeratorId);
                state.GetLocal(idx1) = list != null ? list.GetValue(key) : DreamValue.Null;
            }
            else state.PC = jumpAddress;
        }
        else if (refType1 == DMReference.Type.Argument && (DMReference.Type)state.BytecodePtr[state.PC + 5] == DMReference.Type.Argument)
        {
            state.PC++;
            int idx1 = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;
            state.PC++;
            int idx2 = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;
            int jumpAddress = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;

            var enumerator = state.Thread.GetEnumerator(enumeratorId);
            if (enumerator != null && enumerator.MoveNext())
            {
                var key = enumerator.Current;
                state.GetArgument(idx2) = key;
                var list = state.Thread.GetEnumeratorList(enumeratorId);
                state.GetArgument(idx1) = list != null ? list.GetValue(key) : DreamValue.Null;
            }
            else state.PC = jumpAddress;
        }
        else
        {
            var assocRef = state.ReadReference();
            var outputRef = state.ReadReference();
            int jumpAddress = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;
            var enumerator = state.Thread.GetEnumerator(enumeratorId);
            if (enumerator != null && enumerator.MoveNext())
            {
                var key = enumerator.Current;
                state.Thread.SetReferenceValue(outputRef, ref state.Frame, key, 0);
                state.Thread.PopCount(state.Thread.GetReferenceStackSize(outputRef));
                DreamValue value = DreamValue.Null;
                var list = state.Thread.GetEnumeratorList(enumeratorId);
                if (list != null) value = list.GetValue(key);
                state.Thread.SetReferenceValue(assocRef, ref state.Frame, value, 0);
                state.Thread.PopCount(state.Thread.GetReferenceStackSize(assocRef));
            }
            else state.PC = jumpAddress;
        }
    }

    private static void HandleDestroyEnumerator(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_DestroyEnumerator(state.Proc, ref state.PC);
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleAppend(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_Append(state.Proc, ref state.Frame, ref state.PC);
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleRemove(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_Remove(state.Proc, ref state.Frame, ref state.PC);
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleDeleteObject(ref InterpreterState state)
    {
        var value = state.Pop();
        if (value.TryGetValueAsGameObject(out var obj)) state.Thread.Context!.GameState?.RemoveGameObject(obj);
    }

    private static void HandleProb(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during Prob", state.Proc, state.PC, state.Thread);
        var chanceValue = state.Pop();
        if (chanceValue.TryGetValue(out double chance))
        {
            state.Push(new DreamValue(Random.Shared.NextDouble() * 100 < chance ? 1 : 0));
        }
        else
        {
            state.Push(new DreamValue(0));
        }
    }

    private static void HandleIsSaved(ref InterpreterState state)
    {
        state.Push(DreamValue.True);
    }

    private static void HandleGetStep(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_GetStep();
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleGetStepTo(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_GetStepTo();
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleGetDist(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during GetDist", state.Proc, state.PC, state.Thread);
        var b = state.Pop();
        var a = state.Pop();

        if (a.TryGetValueAsGameObject(out var objA) && b.TryGetValueAsGameObject(out var objB))
        {
            if (objA.Z != objB.Z)
            {
                state.Push(new DreamValue(1000000.0));
                return;
            }
            var dx = Math.Abs(objA.X - objB.X);
            var dy = Math.Abs(objA.Y - objB.Y);
            state.Push(new DreamValue((double)Math.Max(dx, dy)));
        }
        else
        {
            state.Push(new DreamValue(0.0));
        }
    }

    private static void HandleGetDir(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_GetDir();
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleMassConcatenation(ref InterpreterState state)
    {
        int count = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;
        if (count < 0 || count > state.StackPtr)
            throw new ScriptRuntimeException($"Invalid concatenation count: {count}", state.Proc, state.PC, state.Thread);

        if (count == 0)
        {
            state.Push(new DreamValue(""));
            return;
        }

        int baseIdx = state.StackPtr - count;
        var result = _formatStringBuilder.Value!;
        result.Clear();

        for (int i = 0; i < count; i++)
        {
            state.GetStack(baseIdx + i).AppendTo(result);
            if (result.Length > 1073741824)
                throw new ScriptRuntimeException("Maximum string length exceeded during concatenation", state.Proc, state.PC, state.Thread);
        }

        state.StackPtr -= count;
        state.Push(new DreamValue(result.ToString()));
    }

    private static void HandleFormatString(ref InterpreterState state)
    {
        int stringId = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;
        int formatCount = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;

        if (stringId < 0 || stringId >= state.Strings.Count)
            throw new ScriptRuntimeException($"Invalid string ID: {stringId}", state.Proc, state.PC, state.Thread);
        if (formatCount < 0 || formatCount > state.StackPtr)
            throw new ScriptRuntimeException($"Invalid format count: {formatCount}", state.Proc, state.PC, state.Thread);

        var formatString = state.Strings[stringId];
        int baseIdx = state.StackPtr - formatCount;

        var result = _formatStringBuilder.Value!;
        result.Clear();
        if (result.Capacity < formatString.Length + formatCount * 8) result.Capacity = formatString.Length + formatCount * 8;

        int valueIndex = 0;
        for (int i = 0; i < formatString.Length; i++)
        {
            char c = formatString[i];
            if (StringFormatEncoder.Decode(c, out var suffix))
            {
                if (StringFormatEncoder.IsInterpolation(suffix))
                {
                    if (valueIndex < formatCount)
                    {
                        state.GetStack(baseIdx + valueIndex++).AppendTo(result);
                        if (result.Length > 1073741824)
                            throw new ScriptRuntimeException("Maximum string length exceeded during formatting", state.Proc, state.PC, state.Thread);
                    }
                }
                else
                {
                    result.Append(c);
                }
            }
            else
            {
                result.Append(c);
            }
        }

        state.StackPtr -= formatCount;
        state.Push(new DreamValue(result.ToString()));
    }

    private static void HandleCreateObject(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_CreateObject(state.Proc, ref state.PC);
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleLocateCoord(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_LocateCoord();
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleLocate(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_Locate();
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleLength(ref InterpreterState state)
    {
        var value = state.Pop();
        DreamValue result;
        if (value.Type == DreamValueType.String && value.TryGetValue(out string? str)) result = new DreamValue(str?.Length ?? 0);
        else if (value.Type == DreamValueType.DreamObject && value.TryGetValue(out DreamObject? obj) && obj is DreamList list) result = new DreamValue(list.Values.Count);
        else result = new DreamValue(0);
        state.Push(result);
    }

    private static void HandleSpawn(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during Spawn", state.Proc, state.PC, state.Thread);
        var address = state.ReadInt32();
        var bodyPc = state.PC;
        state.PC = address;
        var delay = state.Pop();
        state.Thread._stackPtr = state.StackPtr;
        var newThread = new DreamThread(state.Thread, bodyPc);
        if (delay.TryGetValue(out double seconds) && seconds > 0) newThread.Sleep((float)seconds / 10.0f);
        state.Thread.Context!.ScriptHost?.AddThread(newThread);
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleRgb(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_Rgb(state.Proc, ref state.PC);
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleGradient(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_Gradient(state.Proc, ref state.PC);
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleAppendNoPush(ref InterpreterState state)
    {
        var reference = state.ReadReference();
        var value = state.Pop();
        state.Thread._stackPtr = state.StackPtr;
        var listValue = state.Thread.GetReferenceValue(reference, ref state.Frame);
        if (listValue.Type == DreamValueType.DreamObject && listValue.TryGetValue(out DreamObject? obj) && obj is DreamList list) list.AddValue(value);
        state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleCallGlobalProc(ref InterpreterState state)
    {
        int procId = state.ReadInt32();
        int pcForCache = state.PC - 5;
        var argType = (DMCallArgumentsType)state.ReadByte();
        int argStackDelta = state.ReadInt32();

        IDreamProc? targetProc;
        ref var cache = ref state.Proc._inlineCache[pcForCache];
        if (cache.CachedProc != null)
        {
            targetProc = cache.CachedProc;
        }
        else
        {
            if (procId >= 0 && procId < state.Thread.Context!.AllProcs.Count)
            {
                targetProc = state.Thread.Context.AllProcs[procId];
                cache.CachedProc = targetProc;
            }
            else
            {
                targetProc = null;
            }
        }

        if (targetProc == null)
        {
            state.StackPtr -= argStackDelta;
            state.Push(DreamValue.Null);
            return;
        }

        if (targetProc is NativeProc nativeProc)
        {
            int argCount = argStackDelta;
            int stackBase = state.StackPtr - argStackDelta;
            var arguments = state.StackSpan.Slice(state.StackPtr - argCount, argCount);

            state.StackPtr = stackBase;
            state.Push(nativeProc.Call(state.Thread, null, arguments));
            return;
        }

        state.Thread.SavePC(state.PC);
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.PerformCall(targetProc, null, argStackDelta, argStackDelta);
    }
}
