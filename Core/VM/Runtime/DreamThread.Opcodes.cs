using System;
using System.Collections.Generic;
using Core.VM.Procs;
using Core.VM.Objects;
using System.Linq;

using Shared;

namespace Core.VM.Runtime
{
    public partial class DreamThread
    {
        #region Opcode Handlers
        private void Opcode_PushString(DreamProc proc, ref int pc)
        {
            var stringId = ReadInt32(proc, ref pc);
            Push(new DreamValue(_context.Strings[stringId]));
        }

        private void Opcode_PushFloat(DreamProc proc, ref int pc)
        {
            var value = ReadSingle(proc, ref pc);
            Push(new DreamValue(value));
        }

        private void Opcode_Add()
        {
            var b = _stack[--_stackPtr];
            _stack[_stackPtr - 1] += b;
        }
        private void Opcode_Subtract()
        {
            var b = _stack[--_stackPtr];
            _stack[_stackPtr - 1] -= b;
        }
        private void Opcode_Multiply()
        {
            var b = _stack[--_stackPtr];
            _stack[_stackPtr - 1] *= b;
        }
        private void Opcode_Divide()
        {
            var b = _stack[--_stackPtr];
            _stack[_stackPtr - 1] /= b;
        }

        private void Opcode_CompareEquals()
        {
            var b = _stack[--_stackPtr];
            _stack[_stackPtr - 1] = new DreamValue(_stack[_stackPtr - 1] == b ? 1 : 0);
        }
        private void Opcode_CompareNotEquals()
        {
            var b = _stack[--_stackPtr];
            _stack[_stackPtr - 1] = new DreamValue(_stack[_stackPtr - 1] != b ? 1 : 0);
        }
        private void Opcode_CompareLessThan()
        {
            var b = _stack[--_stackPtr];
            _stack[_stackPtr - 1] = new DreamValue(_stack[_stackPtr - 1] < b ? 1 : 0);
        }
        private void Opcode_CompareGreaterThan()
        {
            var b = _stack[--_stackPtr];
            _stack[_stackPtr - 1] = new DreamValue(_stack[_stackPtr - 1] > b ? 1 : 0);
        }
        private void Opcode_CompareLessThanOrEqual()
        {
            var b = _stack[--_stackPtr];
            _stack[_stackPtr - 1] = new DreamValue(_stack[_stackPtr - 1] <= b ? 1 : 0);
        }
        private void Opcode_CompareGreaterThanOrEqual()
        {
            var b = _stack[--_stackPtr];
            _stack[_stackPtr - 1] = new DreamValue(_stack[_stackPtr - 1] >= b ? 1 : 0);
        }

        private void Opcode_Negate()
        {
            _stack[_stackPtr - 1] = -_stack[_stackPtr - 1];
        }

        private void Opcode_BooleanNot()
        {
            _stack[_stackPtr - 1] = !_stack[_stackPtr - 1];
        }

        private void Opcode_PushNull()
        {
            Push(DreamValue.Null);
        }

        private void Opcode_Pop()
        {
            Pop();
        }

        private void Opcode_Call(DreamProc proc, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            var argType = (DMCallArgumentsType)ReadByte(proc, ref pc);
            var argStackDelta = ReadInt32(proc, ref pc);
            var unusedStackDelta = ReadInt32(proc, ref pc);

            PerformCall(reference, argType, argStackDelta);
        }

        private void Opcode_CallStatement(DreamProc proc, ref int pc)
        {
            var argType = (DMCallArgumentsType)ReadByte(proc, ref pc);
            var argStackDelta = ReadInt32(proc, ref pc);

            _stackPtr -= argStackDelta;
            Push(DreamValue.Null);
        }

        private void Opcode_PushProc(DreamProc proc, ref int pc)
        {
            var procId = ReadInt32(proc, ref pc);
            if (procId >= 0 && procId < _context.AllProcs.Count)
            {
                Push(new DreamValue((IDreamProc)_context.AllProcs[procId]));
            }
            else
            {
                Push(DreamValue.Null);
            }
        }

        private void PerformCall(DMReference reference, DMCallArgumentsType argType, int stackDelta)
        {
            IDreamProc? newProc = null;
            DreamObject? instance = null;

            switch (reference.RefType)
            {
                case DMReference.Type.GlobalProc:
                    if (reference.Index >= 0 && reference.Index < _context.AllProcs.Count)
                        newProc = _context.AllProcs[reference.Index];
                    break;
                case DMReference.Type.SrcProc:
                    var frame = CallStack.Peek();
                    instance = frame.Instance;
                    if (instance != null)
                    {
                        newProc = instance.ObjectType.GetProc(reference.Name);
                        if (newProc == null)
                        {
                            _context.Procs.TryGetValue(reference.Name, out newProc);
                        }
                    }
                    break;
                default:
                    // Handle other reference types (Field, etc.) if they can be called
                    break;
            }

            if (newProc == null)
            {
                State = DreamThreadState.Error;
                throw new Exception($"Attempted to call non-existent proc: {reference}");
            }

            PerformCall(newProc, instance, stackDelta, stackDelta);
        }

        private void Opcode_Jump(DreamProc proc, ref int pc)
        {
            var address = ReadInt32(proc, ref pc);
            pc = address;
        }

        private void Opcode_JumpIfFalse(DreamProc proc, ref int pc)
        {
            var value = Pop();
            var address = ReadInt32(proc, ref pc);
            if (value.IsFalse())
                pc = address;
        }

        private void Opcode_Output()
        {
            var value = Pop();
            Console.WriteLine(value.ToString());
        }

        private void Opcode_Return(ref DreamProc proc, ref int pc)
        {
            var returnValue = Pop();

            var returnedFrame = CallStack.Pop();
            if (CallStack.Count > 0)
            {
                _stackPtr = returnedFrame.StackBase;

                Push(returnValue);

                var newFrame = CallStack.Peek();
                proc = newFrame.Proc;
                pc = newFrame.PC;
            }
            else
            {
                _stackPtr = 0;
                Push(returnValue);
                State = DreamThreadState.Finished;
            }
        }

        private void Opcode_GetVariable(DreamProc proc, CallFrame frame, ref int pc)
        {
            var variableNameId = ReadInt32(proc, ref pc);
            var variableName = _context.Strings[variableNameId];

            var instance = frame.Instance;
            if (instance != null)
            {
                Push(instance.GetVariable(variableName));
            }
            else
            {
                Push(DreamValue.Null);
            }
        }

        private void Opcode_SetVariable(DreamProc proc, CallFrame frame, ref int pc)
        {
            var variableNameId = ReadInt32(proc, ref pc);
            var variableName = _context.Strings[variableNameId];
            var value = Pop();

            var instance = frame.Instance;
            if (instance != null)
            {
                instance.SetVariable(variableName, value);
            }
        }

        private void Opcode_PushReferenceValue(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            Push(GetReferenceValue(reference, frame));
        }

        private void Opcode_Assign(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            var value = _stack[_stackPtr - 1];
            SetReferenceValue(reference, frame, value);
        }

        private GlobalVarsObject? _globalVars;
        private void Opcode_PushGlobalVars()
        {
            _globalVars ??= new GlobalVarsObject(_context);
            Push(new DreamValue(_globalVars));
        }

        private void Opcode_IsNull()
        {
            var value = Pop();
            Push(new DreamValue(value.Type == DreamValueType.Null ? 1 : 0));
        }

        private void Opcode_JumpIfNull(DreamProc proc, ref int pc)
        {
            var value = Pop();
            var address = ReadInt32(proc, ref pc);
            if (value.Type == DreamValueType.Null)
                pc = address;
        }

        private void Opcode_JumpIfNullNoPop(DreamProc proc, ref int pc)
        {
            var value = _stack[_stackPtr - 1];
            var address = ReadInt32(proc, ref pc);
            if (value.Type == DreamValueType.Null)
                pc = address;
        }

        private void Opcode_BitAnd()
        {
            var b = _stack[--_stackPtr];
            _stack[_stackPtr - 1] &= b;
        }
        private void Opcode_BitOr()
        {
            var b = _stack[--_stackPtr];
            _stack[_stackPtr - 1] |= b;
        }
        private void Opcode_BitXor()
        {
            var b = _stack[--_stackPtr];
            _stack[_stackPtr - 1] ^= b;
        }
        private void Opcode_BitNot() => _stack[_stackPtr - 1] = ~_stack[_stackPtr - 1];
        private void Opcode_BitShiftLeft()
        {
            var b = _stack[--_stackPtr];
            _stack[_stackPtr - 1] <<= b;
        }
        private void Opcode_BitShiftRight()
        {
            var b = _stack[--_stackPtr];
            _stack[_stackPtr - 1] >>= b;
        }

        private void Opcode_BooleanAnd(DreamProc proc, ref int pc)
        {
            var value = Pop();
            var jumpAddress = ReadInt32(proc, ref pc);
            if (value.IsFalse())
            {
                Push(value);
                pc = jumpAddress;
            }
        }

        private void Opcode_BooleanOr(DreamProc proc, ref int pc)
        {
            var value = Pop();
            var jumpAddress = ReadInt32(proc, ref pc);
            if (!value.IsFalse())
            {
                Push(value);
                pc = jumpAddress;
            }
        }

        private void Opcode_Increment(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            var value = GetReferenceValue(reference, frame);
            var newValue = value + 1;
            SetReferenceValue(reference, frame, newValue);
            Push(newValue);
        }

        private void Opcode_Decrement(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            var value = GetReferenceValue(reference, frame);
            var newValue = value - 1;
            SetReferenceValue(reference, frame, newValue);
            Push(newValue);
        }

        private void Opcode_Modulus()
        {
            var b = _stack[--_stackPtr];
            _stack[_stackPtr - 1] = new DreamValue((float)Math.IEEERemainder(_stack[_stackPtr - 1].AsFloat(), b.AsFloat()));
        }

        private void Opcode_AssignNoPush(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            var value = Pop();
            SetReferenceValue(reference, frame, value);
        }

        private void Opcode_CreateList(DreamProc proc, ref int pc)
        {
            var size = ReadInt32(proc, ref pc);
            var list = new DreamList(_context.ListType!, size);
            for (int i = size - 1; i >= 0; i--)
            {
                list.Values[i] = Pop();
            }
            Push(new DreamValue(list));
        }

        private void Opcode_IsInList()
        {
            var listValue = Pop();
            var value = Pop();

            if (listValue.Type == DreamValueType.DreamObject && listValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
            {
                Push(new DreamValue(list.Values.Contains(value) ? 1 : 0));
            }
            else
            {
                Push(new DreamValue(0));
            }
        }

        private void Opcode_DereferenceField(DreamProc proc, ref int pc)
        {
            var fieldNameId = ReadInt32(proc, ref pc);
            var fieldName = _context.Strings[fieldNameId];
            var objValue = Pop();

            if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj != null)
            {
                Push(obj.GetVariable(fieldName));
            }
            else
            {
                Push(DreamValue.Null);
            }
        }

        private void Opcode_DereferenceIndex()
        {
            var indexValue = Pop();
            var objValue = Pop();

            if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
            {
                if (indexValue.TryGetValue(out float indexFloat))
                {
                    int index = (int)indexFloat - 1; // DM indices are 1-based
                    if (index >= 0 && index < list.Values.Count)
                    {
                        Push(list.Values[index]);
                        return;
                    }
                }
            }
            Push(DreamValue.Null);
        }

        private void Opcode_DereferenceCall(DreamProc proc, ref int pc)
        {
            var procNameId = ReadInt32(proc, ref pc);
            var procName = _context.Strings[procNameId];
            var argType = (DMCallArgumentsType)ReadByte(proc, ref pc);
            var argStackDelta = ReadInt32(proc, ref pc);

            var objValue = _stack[_stackPtr - argStackDelta];
            if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
            {
                var targetProc = obj.ObjectType.GetProc(procName);
                if (targetProc != null)
                {
                    PerformCall(targetProc, obj, argStackDelta, argStackDelta - 1);
                    return;
                }
            }

            // Fallback: pop everything and push null
            _stackPtr -= argStackDelta;
            Push(DreamValue.Null);
        }

        private void PerformCall(IDreamProc newProc, DreamObject? instance, int stackDelta, int argCount)
        {
            var stackBase = _stackPtr - stackDelta;

            if (newProc is DreamProc dreamProc)
            {
                var frame = new CallFrame(dreamProc, 0, stackBase, instance);
                CallStack.Push(frame);

                for (int i = 0; i < dreamProc.LocalVariableCount; i++)
                {
                    Push(DreamValue.Null);
                }
            }
            else if (newProc is NativeProc nativeProc)
            {
                var arguments = new DreamValue[argCount];
                var argBase = _stackPtr - argCount;
                for (int i = 0; i < argCount; i++)
                {
                    arguments[i] = _stack[argBase + i];
                }

                // Remove everything (args + optional obj) from stack
                _stackPtr = stackBase;

                var result = nativeProc.Call(this, instance, arguments);
                Push(result);
            }
        }

        private void Opcode_Initial()
        {
            var key = Pop();
            var objValue = Pop();

            if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
            {
                if (key.TryGetValue(out string? varName) && varName != null)
                {
                    int index = obj.ObjectType.GetVariableIndex(varName);
                    if (index != -1 && index < obj.ObjectType.FlattenedDefaultValues.Count)
                    {
                        Push(DreamValue.FromObject(obj.ObjectType.FlattenedDefaultValues[index]));
                        return;
                    }
                }
            }
            Push(DreamValue.Null);
        }

        private void Opcode_IsType()
        {
            var typeValue = Pop();
            var objValue = Pop();

            if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj != null &&
                typeValue.Type == DreamValueType.DreamType && typeValue.TryGetValue(out ObjectType? type) && type != null)
            {
                Push(new DreamValue(obj.ObjectType.IsSubtypeOf(type) ? 1 : 0));
            }
            else
            {
                Push(new DreamValue(0));
            }
        }

        private void Opcode_AsType()
        {
            var typeValue = Pop();
            var objValue = Pop();

            if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj != null &&
                typeValue.Type == DreamValueType.DreamType && typeValue.TryGetValue(out ObjectType? type) && type != null)
            {
                if (obj.ObjectType.IsSubtypeOf(type))
                {
                    Push(objValue);
                }
                else
                {
                    Push(DreamValue.Null);
                }
            }
            else
            {
                Push(DreamValue.Null);
            }
        }

        private void Opcode_CreateListEnumerator(DreamProc proc, ref int pc)
        {
            var enumeratorId = ReadInt32(proc, ref pc);
            var listValue = Pop();

            if (listValue.Type == DreamValueType.DreamObject && listValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
            {
                ActiveEnumerators[enumeratorId] = list.Values.GetEnumerator();
            }
            else
            {
                ActiveEnumerators[enumeratorId] = Enumerable.Empty<DreamValue>().GetEnumerator();
            }
        }

        private void Opcode_Enumerate(DreamProc proc, CallFrame frame, ref int pc)
        {
            var enumeratorId = ReadInt32(proc, ref pc);
            var reference = ReadReference(proc, ref pc);
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

        private void Opcode_DestroyEnumerator(DreamProc proc, ref int pc)
        {
            var enumeratorId = ReadInt32(proc, ref pc);
            if (ActiveEnumerators.TryGetValue(enumeratorId, out var enumerator))
            {
                enumerator.Dispose();
                ActiveEnumerators.Remove(enumeratorId);
            }
        }

        private void Opcode_Append(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            var value = Pop();
            var listValue = GetReferenceValue(reference, frame);

            if (listValue.Type == DreamValueType.DreamObject && listValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
            {
                list.Values.Add(value);
            }
        }

        private void Opcode_Remove(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            var value = Pop();
            var listValue = GetReferenceValue(reference, frame);

            if (listValue.Type == DreamValueType.DreamObject && listValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
            {
                list.Values.Remove(value);
            }
        }

        private void Opcode_Prob()
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

        private void Opcode_MassConcatenation(DreamProc proc, ref int pc)
        {
            var count = ReadInt32(proc, ref pc);
            var strings = new string[count];
            for (int i = count - 1; i >= 0; i--)
            {
                strings[i] = Pop().ToString();
            }
            Push(new DreamValue(string.Concat(strings)));
        }

        private void Opcode_FormatString(DreamProc proc, ref int pc)
        {
            var stringId = ReadInt32(proc, ref pc);
            var formatCount = ReadInt32(proc, ref pc);
            var formatString = _context.Strings[stringId];

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

        private void Opcode_Power()
        {
            var b = _stack[--_stackPtr];
            _stack[_stackPtr - 1] = new DreamValue(MathF.Pow(_stack[_stackPtr - 1].AsFloat(), b.AsFloat()));
        }
        private void Opcode_Sqrt() => _stack[_stackPtr - 1] = new DreamValue(SharedOperations.Sqrt(_stack[_stackPtr - 1].AsFloat()));
        private void Opcode_Abs() => _stack[_stackPtr - 1] = new DreamValue(SharedOperations.Abs(_stack[_stackPtr - 1].AsFloat()));
        private void Opcode_Sin() => _stack[_stackPtr - 1] = new DreamValue(SharedOperations.Sin(_stack[_stackPtr - 1].AsFloat()));
        private void Opcode_Cos() => _stack[_stackPtr - 1] = new DreamValue(SharedOperations.Cos(_stack[_stackPtr - 1].AsFloat()));
        private void Opcode_Tan() => _stack[_stackPtr - 1] = new DreamValue(SharedOperations.Tan(_stack[_stackPtr - 1].AsFloat()));
        private void Opcode_ArcSin() => _stack[_stackPtr - 1] = new DreamValue(SharedOperations.ArcSin(_stack[_stackPtr - 1].AsFloat()));
        private void Opcode_ArcCos() => _stack[_stackPtr - 1] = new DreamValue(SharedOperations.ArcCos(_stack[_stackPtr - 1].AsFloat()));
        private void Opcode_ArcTan() => _stack[_stackPtr - 1] = new DreamValue(SharedOperations.ArcTan(_stack[_stackPtr - 1].AsFloat()));
        private void Opcode_ArcTan2()
        {
            var y = _stack[--_stackPtr];
            _stack[_stackPtr - 1] = new DreamValue(SharedOperations.ArcTan(_stack[_stackPtr - 1].AsFloat(), y.AsFloat()));
        }

        private void Opcode_PushType(DreamProc proc, ref int pc)
        {
            var typeId = ReadInt32(proc, ref pc);
            var type = _context.ObjectTypeManager?.GetObjectType(typeId);
            if (type != null)
            {
                Push(new DreamValue(type));
            }
            else
            {
                Push(DreamValue.Null);
            }
        }

        private void Opcode_CreateObject(DreamProc proc, ref int pc)
        {
            var argType = (DMCallArgumentsType)ReadByte(proc, ref pc);
            var argStackDelta = ReadInt32(proc, ref pc);

            var argCount = argStackDelta - 1;
            var values = new DreamValue[argCount];
            for (int i = argCount - 1; i >= 0; i--)
            {
                values[i] = Pop();
            }

            var typeValue = Pop();
            if (typeValue.Type == DreamValueType.DreamType && typeValue.TryGetValue(out ObjectType? type) && type != null)
            {
                var newObj = new GameObject(type);
                _context.GameState?.AddGameObject(newObj);

                Push(new DreamValue(newObj));

                var newProc = newObj.ObjectType.GetProc("New");
                if (newProc != null)
                {
                    // Push arguments for New
                    for (int i = 0; i < argCount; i++)
                    {
                        Push(values[i]);
                    }
                    PerformCall(newProc, newObj, argCount + 1, argCount);
                }
            }
            else
            {
                Push(DreamValue.Null);
            }
        }

        private void Opcode_LocateCoord()
        {
            var zValue = Pop();
            var yValue = Pop();
            var xValue = Pop();

            if (xValue.TryGetValue(out float x) && yValue.TryGetValue(out float y) && zValue.TryGetValue(out float z))
            {
                var turf = _context.GameState?.Map?.GetTurf((int)x, (int)y, (int)z);
                if (turf != null)
                {
                    // For now, we assume we need to wrap Turf in a DreamValue.
                    // If Turf is not a DreamObject, we might need a wrapper.
                    // Looking at existing code, Turf seems to be a specialized object.
                    Push(new DreamValue((DreamObject)(object)turf));
                    return;
                }
            }
            Push(DreamValue.Null);
        }

        private void Opcode_Locate()
        {
            var containerValue = Pop();
            var typeValue = Pop();

            if (typeValue.Type == DreamValueType.DreamType && typeValue.TryGetValue(out ObjectType? type) && type != null)
            {
                if (containerValue.Type == DreamValueType.DreamObject && containerValue.TryGetValue(out DreamObject? container) && container is DreamList list)
                {
                    foreach (var item in list.Values)
                    {
                        if (item.Type == DreamValueType.DreamObject && item.TryGetValue(out DreamObject? obj) && obj != null && obj.ObjectType.IsSubtypeOf(type))
                        {
                            Push(item);
                            return;
                        }
                    }
                }
            }
            Push(DreamValue.Null);
        }

        private void Opcode_Length()
        {
            var value = Pop();
            if (value.Type == DreamValueType.String && value.TryGetValue(out string? str))
            {
                Push(new DreamValue(str?.Length ?? 0));
            }
            else if (value.Type == DreamValueType.DreamObject && value.TryGetValue(out DreamObject? obj) && obj is DreamList list)
            {
                Push(new DreamValue(list.Values.Count));
            }
            else
            {
                Push(new DreamValue(0));
            }
        }

        private void Opcode_Throw()
        {
            var value = Pop();
            State = DreamThreadState.Error;
            Console.WriteLine($"Script threw an error: {value}");
        }

        private void Opcode_Spawn(DreamProc proc, ref int pc)
        {
            var address = ReadInt32(proc, ref pc);
            var delay = Pop();

            var newThread = new DreamThread(this, address);
            if (delay.TryGetValue(out float seconds))
            {
                newThread.Sleep(seconds / 10.0f);
            }
            _context.ScriptHost?.AddThread(newThread);
        }

        private void Opcode_Rgb(DreamProc proc, ref int pc)
        {
            var argType = (DMCallArgumentsType)ReadByte(proc, ref pc);
            var argCount = ReadInt32(proc, ref pc);

            var values = new (string? Name, float? Value)[argCount];
            if (argType == DMCallArgumentsType.FromStackKeyed)
            {
                for (int i = argCount - 1; i >= 0; i--)
                {
                    var value = Pop();
                    var name = Pop().ToString();
                    values[i] = (name, value.AsFloat());
                }
            }
            else
            {
                for (int i = argCount - 1; i >= 0; i--)
                {
                    values[i] = (null, Pop().AsFloat());
                }
            }

            Push(new DreamValue(SharedOperations.ParseRgb(values)));
        }

        private void Opcode_Gradient(DreamProc proc, ref int pc)
        {
            var argType = (DMCallArgumentsType)ReadByte(proc, ref pc);
            var argCount = ReadInt32(proc, ref pc);

            // Stub for gradient
            for (int i = 0; i < argCount; i++) Pop();
            Push(new DreamValue("#000000"));
        }

        private void Opcode_AppendNoPush(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            var value = Pop();
            var listValue = GetReferenceValue(reference, frame);

            if (listValue.Type == DreamValueType.DreamObject && listValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
            {
                list.Values.Add(value);
            }
        }

        private void Opcode_PushRefAndDereferenceField(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            var fieldNameId = ReadInt32(proc, ref pc);
            var fieldName = _context.Strings[fieldNameId];

            var objValue = GetReferenceValue(reference, frame);
            if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj != null)
            {
                Push(obj.GetVariable(fieldName));
            }
            else
            {
                Push(DreamValue.Null);
            }
        }

        private void Opcode_PushNRefs(DreamProc proc, CallFrame frame, ref int pc)
        {
            var count = ReadInt32(proc, ref pc);
            for (int i = 0; i < count; i++)
            {
                var reference = ReadReference(proc, ref pc);
                Push(GetReferenceValue(reference, frame));
            }
        }

        private void Opcode_PushNFloats(DreamProc proc, ref int pc)
        {
            var count = ReadInt32(proc, ref pc);
            for (int i = 0; i < count; i++)
            {
                Push(new DreamValue(ReadSingle(proc, ref pc)));
            }
        }

        private void Opcode_PushStringFloat(DreamProc proc, ref int pc)
        {
            var stringId = ReadInt32(proc, ref pc);
            var value = ReadSingle(proc, ref pc);
            Push(new DreamValue(_context.Strings[stringId]));
            Push(new DreamValue(value));
        }

        private void Opcode_JumpIfReferenceFalse(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            var address = ReadInt32(proc, ref pc);
            if (GetReferenceValue(reference, frame).IsFalse())
            {
                pc = address;
            }
        }

        private void Opcode_ReturnFloat(DreamProc proc, ref int pc)
        {
            var value = ReadSingle(proc, ref pc);
            Push(new DreamValue(value));
            Opcode_Return(ref proc, ref pc);
        }

        private void Opcode_NPushFloatAssign(DreamProc proc, CallFrame frame, ref int pc)
        {
            var count = ReadInt32(proc, ref pc);
            for (int i = 0; i < count; i++)
            {
                var value = ReadSingle(proc, ref pc);
                var reference = ReadReference(proc, ref pc);
                var val = new DreamValue(value);
                SetReferenceValue(reference, frame, val);
                Push(val);
            }
        }

        private void Opcode_IsTypeDirect(DreamProc proc, ref int pc)
        {
            var typeId = ReadInt32(proc, ref pc);
            var type = _context.ObjectTypeManager?.GetObjectType(typeId);
            var objValue = Pop();

            if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj != null && type != null)
            {
                Push(new DreamValue(obj.ObjectType.IsSubtypeOf(type) ? 1 : 0));
            }
            else
            {
                Push(new DreamValue(0));
            }
        }

        private void Opcode_NullRef(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            SetReferenceValue(reference, frame, DreamValue.Null);
        }
        #endregion
    }
}
