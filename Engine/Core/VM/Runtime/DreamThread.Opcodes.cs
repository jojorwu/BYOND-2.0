using Shared.Enums;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Core.VM.Procs;
using Core.VM.Objects;
using System.Linq;
using Shared;

namespace Core.VM.Runtime;

public partial class DreamThread
{
    #region Opcode Handlers

    internal void Opcode_GetStepTo()
    {
        var minDistValue = Pop();
        var targetValue = Pop();
        var srcValue = Pop();

        if (srcValue.TryGetValueAsGameObject(out var src) && targetValue.TryGetValueAsGameObject(out var target))
        {
            var minDist = minDistValue.GetValueAsFloat();
            var dx = target.X - src.X;
            var dy = target.Y - src.Y;

            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) <= minDist)
            {
                Push(DreamValue.Null);
                return;
            }

            int stepX = Math.Sign(dx);
            int stepY = Math.Sign(dy);

            var turf = Context.GameState?.Map?.GetTurf(src.X + stepX, src.Y + stepY, src.Z);
            Push(turf != null ? new DreamValue((GameObject)turf) : DreamValue.Null);
        }
        else
        {
            Push(DreamValue.Null);
        }
    }

    internal void Opcode_GetDist()
    {
        var b = Pop();
        var a = Pop();

        if (a.TryGetValueAsGameObject(out var objA) && b.TryGetValueAsGameObject(out var objB))
        {
            if (objA.Z != objB.Z)
            {
                Push(new DreamValue(1000000.0f));
                return;
            }
            var dx = Math.Abs(objA.X - objB.X);
            var dy = Math.Abs(objA.Y - objB.Y);
            Push(new DreamValue((float)Math.Max(dx, dy)));
        }
        else
        {
            Push(new DreamValue(0.0f));
        }
    }

    internal void PerformCall(DMReference reference, DMCallArgumentsType argType, int stackDelta, bool discardReturnValue = false)
    {
        IDreamProc? newProc = null;
        DreamObject? instance = null;

        switch (reference.RefType)
        {
            case DMReference.Type.GlobalProc:
                if (reference.Index >= 0 && reference.Index < Context.AllProcs.Count)
                    newProc = Context.AllProcs[reference.Index];
                break;
            case DMReference.Type.SrcProc:
                var frame = _callStack[_callStackPtr - 1];
                instance = frame.Instance;
                if (instance != null)
                {
                    newProc = instance.ObjectType?.GetProc(reference.Name);
                    if (newProc == null)
                    {
                        Context.Procs.TryGetValue(reference.Name, out newProc);
                    }
                }
                break;
            default:
                {
                    var value = GetReferenceValue(reference, _callStack[_callStackPtr - 1]);
                    if (value.TryGetValue(out IDreamProc? proc))
                    {
                        newProc = proc;
                    }
                }
                break;
        }

        if (newProc == null)
        {
            State = DreamThreadState.Error;
            throw new Exception($"Attempted to call non-existent proc: {reference}");
        }

        PerformCall(newProc, instance, stackDelta, stackDelta, discardReturnValue);
    }

    internal void Opcode_OutputReference(DreamProc proc, CallFrame frame, ref int pc)
    {
        var reference = ReadReference(proc.Bytecode, ref pc);
        var message = Pop();
        var target = GetReferenceValue(reference, frame, 0);
        PopCount(GetReferenceStackSize(reference));

        if (!message.IsNull)
        {
            // TODO: Proper output routing based on target
            Console.WriteLine(message.ToString());
        }
    }

    internal void Opcode_Return(ref DreamProc proc, ref int pc)
    {
        var returnValue = Pop();

        var returnedFrame = PopCallFrame();
        _stackPtr = returnedFrame.StackBase;

        if (_callStackPtr > 0)
        {
            if (!returnedFrame.DiscardReturnValue)
            {
                Push(returnValue);
            }

            var newFrame = _callStack[_callStackPtr - 1];
            proc = newFrame.Proc;
            pc = newFrame.PC;
        }
        else
        {
            Push(returnValue);
            State = DreamThreadState.Finished;
        }
    }

    private GlobalVarsObject? _globalVars;
    internal void Opcode_PushGlobalVars()
    {
        _globalVars ??= new GlobalVarsObject(Context);
        Push(new DreamValue(_globalVars));
    }

    internal void Opcode_Modulus()
    {
        var b = Pop();
        var a = Pop();
        Push(a % b);
    }

    internal void Opcode_ModulusModulus()
    {
        var b = Pop();
        var a = Pop();
        Push(new DreamValue(SharedOperations.Modulo(a.GetValueAsFloat(), b.GetValueAsFloat())));
    }

    internal void Opcode_GetStep()
    {
        var dirValue = Pop();
        var objValue = Pop();

        if (objValue.TryGetValue(out DreamObject? obj) && obj is GameObject gameObject)
        {
            var dir = (int)dirValue.GetValueAsFloat();
            int dx = 0, dy = 0;
            if ((dir & 1) != 0) dy++; // NORTH
            if ((dir & 2) != 0) dy--; // SOUTH
            if ((dir & 4) != 0) dx++; // EAST
            if ((dir & 8) != 0) dx--; // WEST

            var turf = Context.GameState?.Map?.GetTurf(gameObject.X + dx, gameObject.Y + dy, gameObject.Z);
            Push(turf != null ? new DreamValue((GameObject)turf) : DreamValue.Null);
        }
        else
        {
            Push(DreamValue.Null);
        }
    }

    internal void Opcode_GetDir()
    {
        var targetValue = Pop();
        var sourceValue = Pop();

        if (sourceValue.TryGetValue(out DreamObject? src) && src is GameObject srcObj &&
            targetValue.TryGetValue(out DreamObject? dst) && dst is GameObject dstObj)
        {
            int dx = dstObj.X - srcObj.X;
            int dy = dstObj.Y - srcObj.Y;

            int dir = 0;
            if (dy > 0) dir |= 1; // NORTH
            else if (dy < 0) dir |= 2; // SOUTH
            if (dx > 0) dir |= 4; // EAST
            else if (dx < 0) dir |= 8; // WEST

            Push(new DreamValue((float)dir));
        }
        else
        {
            Push(new DreamValue(0f));
        }
    }

    internal void Opcode_PickUnweighted(DreamProc proc, ref int pc)
    {
        var count = ReadInt32(proc, ref pc);
        if (count < 0 || count > _stackPtr)
            throw new ScriptRuntimeException($"Invalid pick count: {count}", proc, pc, this);
        if (count == 0)
        {
            Push(DreamValue.Null);
            return;
        }

        int index = Random.Shared.Next(0, count);
        var result = _stack[_stackPtr - count + index];
        _stackPtr -= count;
        Push(result);
    }

    internal void Opcode_PickWeighted(DreamProc proc, ref int pc)
    {
        var count = ReadInt32(proc, ref pc);
        if (count < 0 || count * 2 > _stackPtr)
            throw new ScriptRuntimeException($"Invalid weighted pick count: {count}", proc, pc, this);
        if (count == 0)
        {
            Push(DreamValue.Null);
            return;
        }

        float totalWeight = 0;
        int baseIdx = _stackPtr - count * 2;
        for (int i = 0; i < count; i++)
        {
            totalWeight += _stack[baseIdx + i * 2 + 1].GetValueAsFloat();
        }

        if (totalWeight <= 0)
        {
            var result = _stack[baseIdx];
            _stackPtr -= count * 2;
            Push(result);
            return;
        }

        var pick = (float)Random.Shared.NextDouble() * totalWeight;
        float currentWeight = 0;
        for (int i = 0; i < count; i++)
        {
            currentWeight += _stack[baseIdx + i * 2 + 1].GetValueAsFloat();
            if (pick <= currentWeight)
            {
                var result = _stack[baseIdx + i * 2];
                _stackPtr -= count * 2;
                Push(result);
                return;
            }
        }

        var finalResult = _stack[baseIdx + (count - 1) * 2];
        _stackPtr -= count * 2;
        Push(finalResult);
    }

    internal void Opcode_CreateList(DreamProc proc, ref int pc)
    {
        var size = ReadInt32(proc, ref pc);
        if (size < 0 || size > _stackPtr)
            throw new ScriptRuntimeException($"Invalid list size: {size}", proc, pc, this);

        var list = new DreamList(Context.ListType!, 0);
        if (size > 0)
        {
            list.Populate(_stack.AsSpan(_stackPtr - size, size));
            _stackPtr -= size;
        }
        Push(new DreamValue(list));
    }

    internal void Opcode_CreateAssociativeList(DreamProc proc, ref int pc)
    {
        var size = ReadInt32(proc, ref pc);
        if (size < 0 || size * 2 > _stackPtr)
            throw new ScriptRuntimeException($"Invalid associative list size: {size}", proc, pc, this);
        var list = new DreamList(Context.ListType!);
        if (size > 0)
        {
            int baseIdx = _stackPtr - size * 2;
            for (int i = 0; i < size; i++)
            {
                var key = _stack[baseIdx + i * 2];
                var value = _stack[baseIdx + i * 2 + 1];
                list.SetValue(key, value);
            }
            _stackPtr -= size * 2;
        }
        Push(new DreamValue(list));
    }

    internal void Opcode_CreateStrictAssociativeList(DreamProc proc, ref int pc)
    {
        // Same for now
        Opcode_CreateAssociativeList(proc, ref pc);
    }

    internal void Opcode_IsInList()
    {
        var listValue = Pop();
        var value = Pop();

        if (listValue.Type == DreamValueType.DreamObject && listValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
        {
            Push(list.Contains(value) ? DreamValue.True : DreamValue.False);
        }
        else
        {
            Push(DreamValue.False);
        }
    }

    internal void PerformCall(IDreamProc newProc, DreamObject? instance, int stackDelta, int argCount, bool discardReturnValue = false)
    {
        if (_callStackPtr >= MaxCallStackDepth)
            throw new ScriptRuntimeException("Maximum call stack depth exceeded", CurrentProc, _callStack[_callStackPtr - 1].PC, this);

        if (stackDelta < 0 || stackDelta > _stackPtr)
            throw new ScriptRuntimeException($"Invalid stack delta for procedure call: {stackDelta}", CurrentProc, 0, this);

        var stackBase = _stackPtr - stackDelta;

        if (newProc is DreamProc dreamProc)
        {
            var frame = new CallFrame(dreamProc, 0, stackBase, instance, discardReturnValue);
            PushCallFrame(frame);

            for (int i = 0; i < dreamProc.LocalVariableCount; i++)
            {
                Push(DreamValue.Null);
            }
        }
        else if (newProc is NativeProc nativeProc)
        {
            ReadOnlySpan<DreamValue> arguments = _stack.AsSpan(_stackPtr - argCount, argCount);

            // Remove everything (args + optional obj) from stack
            _stackPtr = stackBase;

            var result = nativeProc.Call(this, instance, arguments);
            if (!discardReturnValue)
            {
                Push(result);
            }
        }
    }

    internal void Opcode_CreateListEnumerator(DreamProc proc, ref int pc)
    {
        var enumeratorId = ReadInt32(proc, ref pc);
        var listValue = Pop();

        if (listValue.Type == DreamValueType.DreamObject && listValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
        {
            ActiveEnumerators[enumeratorId] = list.Values.GetEnumerator();
            EnumeratorLists[enumeratorId] = list;
        }
        else
        {
            ActiveEnumerators[enumeratorId] = Enumerable.Empty<DreamValue>().GetEnumerator();
        }
    }

    internal void Opcode_Enumerate(DreamProc proc, CallFrame frame, ref int pc)
    {
        var enumeratorId = ReadInt32(proc, ref pc);
        var reference = ReadReference(proc.Bytecode, ref pc);
        var jumpAddress = ReadInt32(proc, ref pc);

        if (ActiveEnumerators.TryGetValue(enumeratorId, out var enumerator))
        {
            if (enumerator.MoveNext())
            {
                SetReferenceValue(reference, frame, enumerator.Current);
            }
            else
            {
                pc = jumpAddress;
            }
        }
        else
        {
            pc = jumpAddress;
        }
    }

    internal void Opcode_EnumerateAssoc(DreamProc proc, CallFrame frame, ref int pc)
    {
        var enumeratorId = ReadInt32(proc, ref pc);
        var assocRef = ReadReference(proc.Bytecode, ref pc);
        var outputRef = ReadReference(proc.Bytecode, ref pc);
        var jumpAddress = ReadInt32(proc, ref pc);

        if (ActiveEnumerators.TryGetValue(enumeratorId, out var enumerator))
        {
            if (enumerator.MoveNext())
            {
                var key = enumerator.Current;
                SetReferenceValue(outputRef, frame, key);

                DreamValue value = DreamValue.Null;
                if (EnumeratorLists.TryGetValue(enumeratorId, out var list))
                {
                    value = list.GetValue(key);
                }
                SetReferenceValue(assocRef, frame, value);
            }
            else
            {
                pc = jumpAddress;
            }
        }
        else
        {
            pc = jumpAddress;
        }
    }

    internal void Opcode_DestroyEnumerator(DreamProc proc, ref int pc)
    {
        var enumeratorId = ReadInt32(proc, ref pc);
        if (ActiveEnumerators.TryGetValue(enumeratorId, out var enumerator))
        {
            enumerator.Dispose();
            ActiveEnumerators.Remove(enumeratorId);
        }
        EnumeratorLists.Remove(enumeratorId);
    }

    internal void Opcode_Append(DreamProc proc, CallFrame frame, ref int pc)
    {
        var reference = ReadReference(proc.Bytecode, ref pc);
        var value = Pop();
        var listValue = GetReferenceValue(reference, frame);

        if (listValue.Type == DreamValueType.DreamObject && listValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
        {
            list.AddValue(value);
        }
    }

    internal void Opcode_Remove(DreamProc proc, CallFrame frame, ref int pc)
    {
        var reference = ReadReference(proc.Bytecode, ref pc);
        var value = Pop();
        var listValue = GetReferenceValue(reference, frame);

        if (listValue.Type == DreamValueType.DreamObject && listValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
        {
            list.RemoveValue(value);
        }
    }

    internal void Opcode_Prob()
    {
        var chanceValue = Pop();
        if (chanceValue.TryGetValue(out float chance))
        {
            var roll = Random.Shared.NextDouble() * 100;
            Push(new DreamValue(roll < chance ? 1 : 0));
        }
        else
        {
            Push(new DreamValue(0));
        }
    }

    internal void Opcode_MassConcatenation(DreamProc proc, ref int pc)
    {
        var count = ReadInt32(proc, ref pc);
        if (count < 0 || count > _stackPtr)
            throw new ScriptRuntimeException($"Invalid concatenation count: {count}", proc, pc, this);
        if (count > 1024)
            throw new ScriptRuntimeException($"Concatenation count too large: {count}", proc, pc, this);

        if (count == 0)
        {
            Push(new DreamValue(""));
            return;
        }

        int baseIdx = _stackPtr - count;
        var strings = new string[count];
        long totalLength = 0;
        for (int i = 0; i < count; i++)
        {
            strings[i] = _stack[baseIdx + i].ToString();
            totalLength += strings[i].Length;
        }

        if (totalLength > 67108864)
            throw new ScriptRuntimeException("Maximum string length exceeded during concatenation", proc, pc, this);

        _stackPtr -= count;
        Push(new DreamValue(string.Concat(strings)));
    }

    internal void Opcode_FormatString(DreamProc proc, ref int pc)
    {
        var stringId = ReadInt32(proc, ref pc);
        var formatCount = ReadInt32(proc, ref pc);
        if (stringId < 0 || stringId >= Context.Strings.Count)
            throw new ScriptRuntimeException($"Invalid string ID: {stringId}", proc, pc, this);
        if (formatCount < 0 || formatCount > _stackPtr)
            throw new ScriptRuntimeException($"Invalid format count: {formatCount}", proc, pc, this);
        var formatString = Context.Strings[stringId];

        var values = new DreamValue[formatCount];
        for (int i = formatCount - 1; i >= 0; i--)
        {
            values[i] = Pop();
        }

        var result = new System.Text.StringBuilder(formatString.Length + formatCount * 8);
        int valueIndex = 0;

        for (int i = 0; i < formatString.Length; i++)
        {
            char c = formatString[i];
            if (StringFormatEncoder.Decode(c, out var suffix))
            {
                if (StringFormatEncoder.IsInterpolation(suffix))
                {
                    if (valueIndex < values.Length)
                    {
                        // Basic interpolation for now
                        result.Append(values[valueIndex++].ToString());
                        if (result.Length > 67108864)
                            throw new ScriptRuntimeException("Maximum string length exceeded during formatting", proc, pc, this);
                    }
                }
                // Handle other macros if needed (The, the, etc.)
            }
            else
            {
                result.Append(c);
            }
        }

        Push(new DreamValue(result.ToString()));
    }

    internal void Opcode_Power()
    {
        var b = Pop();
        var a = Pop();
        Push(new DreamValue(MathF.Pow(a.GetValueAsFloat(), b.GetValueAsFloat())));
    }
    internal void Opcode_Sqrt()
    {
        var a = Pop();
        Push(new DreamValue(SharedOperations.Sqrt(a.GetValueAsFloat())));
    }
    internal void Opcode_Abs()
    {
        var a = Pop();
        Push(new DreamValue(SharedOperations.Abs(a.GetValueAsFloat())));
    }

    internal void Opcode_Sin() => Push(new DreamValue(SharedOperations.Sin(Pop().GetValueAsFloat())));
    internal void Opcode_Cos() => Push(new DreamValue(SharedOperations.Cos(Pop().GetValueAsFloat())));
    internal void Opcode_Tan() => Push(new DreamValue(SharedOperations.Tan(Pop().GetValueAsFloat())));
    internal void Opcode_ArcSin() => Push(new DreamValue(SharedOperations.ArcSin(Pop().GetValueAsFloat())));
    internal void Opcode_ArcCos() => Push(new DreamValue(SharedOperations.ArcCos(Pop().GetValueAsFloat())));
    internal void Opcode_ArcTan() => Push(new DreamValue(SharedOperations.ArcTan(Pop().GetValueAsFloat())));
    internal void Opcode_ArcTan2()
    {
        var y = Pop();
        var x = Pop();
        Push(new DreamValue(SharedOperations.ArcTan(x.GetValueAsFloat(), y.GetValueAsFloat())));
    }

    internal void Opcode_CreateObject(DreamProc proc, ref int pc)
    {
        var argType = (DMCallArgumentsType)ReadByte(proc, ref pc);
        var argStackDelta = ReadInt32(proc, ref pc);

        if (argStackDelta < 1 || argStackDelta > _stackPtr)
            throw new ScriptRuntimeException($"Invalid argument stack delta: {argStackDelta}", proc, pc, this);

        var argCount = argStackDelta - 1;
        var values = new DreamValue[argCount];
        for (int i = argCount - 1; i >= 0; i--)
        {
            values[i] = Pop();
        }

        var typeValue = Pop();
        if (typeValue.Type == DreamValueType.DreamType && typeValue.TryGetValue(out ObjectType? type) && type != null)
        {
            GameObject newObj;
            if (Context.ObjectFactory != null)
            {
                newObj = Context.ObjectFactory.Create(type);
            }
            else
            {
                newObj = new GameObject(type);
            }

            Context.GameState?.AddGameObject(newObj);

            Push(new DreamValue(newObj));

            if (argCount > 0)
            {
                var locValue = values[0];
                if (locValue.TryGetValueAsGameObject(out var locObj))
                {
                    newObj.Loc = locObj;
                }
            }

            var newProc = newObj.ObjectType?.GetProc("New");
            if (newProc != null)
            {
                // Push arguments for New
                for (int i = 0; i < argCount; i++)
                {
                    Push(values[i]);
                }
                SavePC(pc);
                PerformCall(newProc, newObj, argCount, argCount, discardReturnValue: true);
            }
        }
        else
        {
            Push(DreamValue.Null);
        }
    }

    internal void Opcode_LocateCoord()
    {
        var zValue = Pop();
        var yValue = Pop();
        var xValue = Pop();

        if (xValue.TryGetValue(out float x) && yValue.TryGetValue(out float y) && zValue.TryGetValue(out float z))
        {
            var turf = Context.GameState?.Map?.GetTurf((int)x, (int)y, (int)z);
            if (turf != null)
            {
                // Turf is ITurf, but the implementation inherits from GameObject which is a DreamObject
                Push(new DreamValue((GameObject)turf));
                return;
            }
        }
        Push(DreamValue.Null);
    }

    internal void Opcode_Locate()
    {
        var containerValue = Pop();
        var typeValue = Pop();

        if (typeValue.Type == DreamValueType.DreamType && typeValue.TryGetValue(out ObjectType? type) && type != null)
        {
            if (containerValue.Type == DreamValueType.DreamObject && containerValue.TryGetValue(out DreamObject? container))
            {
                if (container is DreamList list)
                {
                    foreach (var item in list.Values)
                    {
                        if (item.Type == DreamValueType.DreamObject && item.TryGetValue(out DreamObject? obj) && obj?.ObjectType != null && obj.ObjectType.IsSubtypeOf(type))
                        {
                            Push(item);
                            return;
                        }
                    }
                }
                else if (container is GameObject gameObject)
                {
                    foreach (var content in gameObject.Contents)
                    {
                        if (content.ObjectType != null && content.ObjectType.IsSubtypeOf(type) && content is GameObject contentObj)
                        {
                            Push(new DreamValue(contentObj));
                            return;
                        }
                    }
                }
            }
            else if (containerValue.IsNull)
            {
                if (Context.GameState != null)
                {
                    using (Context.GameState.ReadLock())
                    {
                        foreach (var obj in Context.GameState.GameObjects.Values)
                        {
                            if (obj.ObjectType != null && obj.ObjectType.IsSubtypeOf(type))
                            {
                                Push(new DreamValue(obj));
                                return;
                            }
                        }
                    }
                }
            }
        }
        Push(DreamValue.Null);
    }

    internal void Opcode_Rgb(DreamProc proc, ref int pc)
    {
        var argType = (DMCallArgumentsType)ReadByte(proc, ref pc);
        var argCount = ReadInt32(proc, ref pc);

        if (argCount < 0 || (argType == DMCallArgumentsType.FromStackKeyed ? argCount * 2 : argCount) > _stackPtr)
            throw new ScriptRuntimeException($"Invalid rgb argument count: {argCount}", proc, pc, this);

        var values = new (string? Name, float? Value)[argCount];
        if (argType == DMCallArgumentsType.FromStackKeyed)
        {
            for (int i = argCount - 1; i >= 0; i--)
            {
                var value = Pop();
                var name = Pop().ToString();
                values[i] = (name, value.GetValueAsFloat());
            }
        }
        else
        {
            for (int i = argCount - 1; i >= 0; i--)
            {
                values[i] = (null, Pop().GetValueAsFloat());
            }
        }

        Push(new DreamValue(SharedOperations.ParseRgb(values)));
    }

    internal void Opcode_Gradient(DreamProc proc, ref int pc)
    {
        var argType = (DMCallArgumentsType)ReadByte(proc, ref pc);
        var argCount = ReadInt32(proc, ref pc);

        if (argCount < 0 || argCount > _stackPtr)
            throw new ScriptRuntimeException($"Invalid gradient argument count: {argCount}", proc, pc, this);

        var values = new DreamValue[argCount];
        for (int i = argCount - 1; i >= 0; i--)
        {
            values[i] = Pop();
        }

        if (argCount >= 3)
        {
            // Simple 2-color interpolation: gradient(color1, color2, index)
            var color1Str = values[0].ToString();
            var color2Str = values[1].ToString();
            var index = values[2].GetValueAsFloat();

            var c1 = Robust.Shared.Maths.Color.TryFromHex(color1Str);
            var c2 = Robust.Shared.Maths.Color.TryFromHex(color2Str);

            if (c1.HasValue && c2.HasValue)
            {
                var interpolated = Robust.Shared.Maths.Color.InterpolateBetween(c1.Value, c2.Value, Math.Clamp(index / 100f, 0, 1));
                Push(new DreamValue(interpolated.ToHex()));
                return;
            }
        }

        Push(new DreamValue("#000000"));
    }

    #endregion
}
