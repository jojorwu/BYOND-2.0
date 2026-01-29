using System;
using System.Buffers.Binary;
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

        private void Opcode_GetStepTo()
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

                var turf = _context.GameState?.Map?.GetTurf(src.X + stepX, src.Y + stepY, src.Z);
                Push(turf != null ? new DreamValue((GameObject)turf) : DreamValue.Null);
            }
            else
            {
                Push(DreamValue.Null);
            }
        }

        private void Opcode_GetDist()
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


        private void Opcode_OutputReference(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            var value = GetReferenceValue(reference, frame);
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


        private GlobalVarsObject? _globalVars;
        private void Opcode_PushGlobalVars()
        {
            _globalVars ??= new GlobalVarsObject(_context);
            Push(new DreamValue(_globalVars));
        }





        private void Opcode_BitShiftLeftReference(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            var value = Pop();
            var refValue = GetReferenceValue(reference, frame);
            SetReferenceValue(reference, frame, refValue << value);
        }

        private void Opcode_BitShiftRightReference(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            var value = Pop();
            var refValue = GetReferenceValue(reference, frame);
            SetReferenceValue(reference, frame, refValue >> value);
        }

        private void Opcode_BitXorReference(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            var value = Pop();
            var refValue = GetReferenceValue(reference, frame);
            SetReferenceValue(reference, frame, refValue ^ value);
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
            var b = Pop();
            var a = Pop();
            Push(new DreamValue((float)Math.IEEERemainder(a.GetValueAsFloat(), b.GetValueAsFloat())));
        }

        private void Opcode_ModulusModulus()
        {
            var b = Pop();
            var a = Pop();
            Push(new DreamValue(a.GetValueAsFloat() % b.GetValueAsFloat()));
        }

        private void Opcode_ModulusReference(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            var value = Pop();
            var refValue = GetReferenceValue(reference, frame);
            SetReferenceValue(reference, frame, new DreamValue((float)Math.IEEERemainder(refValue.GetValueAsFloat(), value.GetValueAsFloat())));
        }

        private void Opcode_ModulusModulusReference(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            var value = Pop();
            var refValue = GetReferenceValue(reference, frame);
            SetReferenceValue(reference, frame, new DreamValue(refValue.GetValueAsFloat() % value.GetValueAsFloat()));
        }

        private void Opcode_AssignNoPush(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            var value = Pop();
            SetReferenceValue(reference, frame, value);
        }

        private void Opcode_AssignInto(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            var value = Pop();
            SetReferenceValue(reference, frame, value);
            Push(value);
        }

        private void Opcode_GetStep()
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

                var turf = _context.GameState?.Map?.GetTurf(gameObject.X + dx, gameObject.Y + dy, gameObject.Z);
                Push(turf != null ? new DreamValue((GameObject)turf) : DreamValue.Null);
            }
            else
            {
                Push(DreamValue.Null);
            }
        }

        private void Opcode_GetDir()
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

        private void Opcode_PickUnweighted(DreamProc proc, ref int pc)
        {
            var count = ReadInt32(proc, ref pc);
            if (count == 0)
            {
                Push(DreamValue.Null);
                return;
            }

            var values = new DreamValue[count];
            for (int i = count - 1; i >= 0; i--)
                values[i] = Pop();

            var result = values[Random.Shared.Next(0, count)];
            Push(result);
        }

        private void Opcode_PickWeighted(DreamProc proc, ref int pc)
        {
            var count = ReadInt32(proc, ref pc);
            if (count == 0)
            {
                Push(DreamValue.Null);
                return;
            }

            var values = new (DreamValue Value, float Weight)[count];
            float totalWeight = 0;
            for (int i = count - 1; i >= 0; i--)
            {
                var weight = Pop().GetValueAsFloat();
                var val = Pop();
                values[i] = (val, weight);
                totalWeight += weight;
            }

            if (totalWeight <= 0)
            {
                Push(values[0].Value);
                return;
            }

            var pick = (float)Random.Shared.NextDouble() * totalWeight;
            float currentWeight = 0;
            for (int i = 0; i < count; i++)
            {
                currentWeight += values[i].Weight;
                if (pick <= currentWeight)
                {
                    Push(values[i].Value);
                    return;
                }
            }

            Push(values[count - 1].Value);
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

        private void Opcode_CreateAssociativeList(DreamProc proc, ref int pc)
        {
            var size = ReadInt32(proc, ref pc);
            var list = new DreamList(_context.ListType!);
            for (int i = 0; i < size; i++)
            {
                var value = Pop();
                var key = Pop();
                list.SetValue(key, value);
            }
            Push(new DreamValue(list));
        }

        private void Opcode_CreateStrictAssociativeList(DreamProc proc, ref int pc)
        {
            // Same for now
            Opcode_CreateAssociativeList(proc, ref pc);
        }

        private void Opcode_IsInList()
        {
            var listValue = Pop();
            var value = Pop();

            if (listValue.Type == DreamValueType.DreamObject && listValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
            {
                Push(list.Values.Contains(value) ? DreamValue.True : DreamValue.False);
            }
            else
            {
                Push(DreamValue.False);
            }
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
                EnumeratorLists[enumeratorId] = list;
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

        private void Opcode_EnumerateAssoc(DreamProc proc, CallFrame frame, ref int pc)
        {
            var enumeratorId = ReadInt32(proc, ref pc);
            var assocRef = ReadReference(proc, ref pc);
            var outputRef = ReadReference(proc, ref pc);
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

        private void Opcode_DestroyEnumerator(DreamProc proc, ref int pc)
        {
            var enumeratorId = ReadInt32(proc, ref pc);
            if (ActiveEnumerators.TryGetValue(enumeratorId, out var enumerator))
            {
                enumerator.Dispose();
                ActiveEnumerators.Remove(enumeratorId);
            }
            EnumeratorLists.Remove(enumeratorId);
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
            var b = Pop();
            var a = Pop();
            Push(new DreamValue(MathF.Pow(a.GetValueAsFloat(), b.GetValueAsFloat())));
        }
        private void Opcode_Sqrt()
        {
            var a = Pop();
            Push(new DreamValue(SharedOperations.Sqrt(a.GetValueAsFloat())));
        }
        private void Opcode_Abs()
        {
            var a = Pop();
            Push(new DreamValue(SharedOperations.Abs(a.GetValueAsFloat())));
        }

        private void Opcode_MultiplyReference(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            var value = Pop();
            var refValue = GetReferenceValue(reference, frame);
            SetReferenceValue(reference, frame, refValue * value);
        }

        private void Opcode_DivideReference(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            var value = Pop();
            var refValue = GetReferenceValue(reference, frame);
            SetReferenceValue(reference, frame, refValue / value);
        }

        private void Opcode_Sin() => Push(new DreamValue(SharedOperations.Sin(Pop().GetValueAsFloat())));
        private void Opcode_Cos() => Push(new DreamValue(SharedOperations.Cos(Pop().GetValueAsFloat())));
        private void Opcode_Tan() => Push(new DreamValue(SharedOperations.Tan(Pop().GetValueAsFloat())));
        private void Opcode_ArcSin() => Push(new DreamValue(SharedOperations.ArcSin(Pop().GetValueAsFloat())));
        private void Opcode_ArcCos() => Push(new DreamValue(SharedOperations.ArcCos(Pop().GetValueAsFloat())));
        private void Opcode_ArcTan() => Push(new DreamValue(SharedOperations.ArcTan(Pop().GetValueAsFloat())));
        private void Opcode_ArcTan2()
        {
            var y = Pop();
            var x = Pop();
            Push(new DreamValue(SharedOperations.ArcTan(x.GetValueAsFloat(), y.GetValueAsFloat())));
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
                    // Turf is ITurf, but the implementation inherits from GameObject which is a DreamObject
                    Push(new DreamValue((GameObject)turf));
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
                if (containerValue.Type == DreamValueType.DreamObject && containerValue.TryGetValue(out DreamObject? container))
                {
                    if (container is DreamList list)
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
                    else if (container is GameObject gameObject)
                    {
                        foreach (var content in gameObject.Contents)
                        {
                            if (content.ObjectType.IsSubtypeOf(type) && content is GameObject contentObj)
                            {
                                Push(new DreamValue(contentObj));
                                return;
                            }
                        }
                    }
                }
                else if (containerValue.IsNull)
                {
                    if (_context.GameState != null)
                    {
                        foreach (var obj in _context.GameState.GameObjects.Values)
                        {
                            if (obj.ObjectType.IsSubtypeOf(type))
                            {
                                Push(new DreamValue(obj));
                                return;
                            }
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

        private void Opcode_IsInRange()
        {
            var max = Pop();
            var min = Pop();
            var val = Pop();
            Push(new DreamValue(val >= min && val <= max ? 1 : 0));
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

        private void Opcode_Gradient(DreamProc proc, ref int pc)
        {
            var argType = (DMCallArgumentsType)ReadByte(proc, ref pc);
            var argCount = ReadInt32(proc, ref pc);

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

        private void Opcode_SwitchOnFloat(DreamProc proc, ref int pc)
        {
            var value = ReadSingle(proc, ref pc);
            var jumpAddress = ReadInt32(proc, ref pc);
            var switchValue = Peek();
            if (switchValue.Type == DreamValueType.Float && switchValue.AsFloat() == value)
                pc = jumpAddress;
        }

        private void Opcode_SwitchOnString(DreamProc proc, ref int pc)
        {
            var stringId = ReadInt32(proc, ref pc);
            var jumpAddress = ReadInt32(proc, ref pc);
            var switchValue = Peek();
            if (switchValue.Type == DreamValueType.String && switchValue.TryGetValue(out string? s) && s == _context.Strings[stringId])
                pc = jumpAddress;
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

        private void Opcode_IndexRefWithString(DreamProc proc, CallFrame frame, ref int pc)
        {
            var reference = ReadReference(proc, ref pc);
            var stringId = ReadInt32(proc, ref pc);
            var stringValue = new DreamValue(_context.Strings[stringId]);

            var objValue = GetReferenceValue(reference, frame);
            if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
            {
                Push(list.GetValue(stringValue));
            }
            else
            {
                Push(DreamValue.Null);
            }
        }
        #endregion
    }
}
