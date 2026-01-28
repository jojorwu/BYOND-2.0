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

        private void PerformBinaryOperation(Func<DreamValue, DreamValue, DreamValue> operation)
        {
            var b = Stack[^1];
            var a = Stack[^2];
            Stack.RemoveAt(Stack.Count - 1);
            Stack[^1] = operation(a, b);
        }
        private void Opcode_Add() => PerformBinaryOperation((a, b) => a + b);
        private void Opcode_Subtract() => PerformBinaryOperation((a, b) => a - b);
        private void Opcode_Multiply() => PerformBinaryOperation((a, b) => a * b);
        private void Opcode_Divide() => PerformBinaryOperation((a, b) => a / b);

        private void PerformComparisonOperation(Func<DreamValue, DreamValue, bool> operation)
        {
            var b = Stack[^1];
            var a = Stack[^2];
            Stack.RemoveAt(Stack.Count - 1);
            Stack[^1] = new DreamValue(operation(a, b) ? 1 : 0);
        }

        private void Opcode_CompareEquals() => PerformComparisonOperation((a, b) => a == b);
        private void Opcode_CompareNotEquals() => PerformComparisonOperation((a, b) => a != b);
        private void Opcode_CompareLessThan() => PerformComparisonOperation((a, b) => a < b);
        private void Opcode_CompareGreaterThan() => PerformComparisonOperation((a, b) => a > b);
        private void Opcode_CompareLessThanOrEqual() => PerformComparisonOperation((a, b) => a <= b);
        private void Opcode_CompareGreaterThanOrEqual() => PerformComparisonOperation((a, b) => a >= b);

        private void Opcode_Negate()
        {
            Stack[^1] = -Stack[^1];
        }

        private void Opcode_BooleanNot()
        {
            Stack[^1] = !Stack[^1];
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
            var procId = ReadInt32(proc, ref pc);
            var argCount = ReadByte(proc, ref pc);

            var procName = _context.Strings[procId];
            if (!_context.Procs.TryGetValue(procName, out var newProc))
            {
                State = DreamThreadState.Error;
                throw new Exception($"Attempted to call non-existent proc: {procName}");
            }

            var stackBase = Stack.Count - argCount;
            var instanceValue = Stack[stackBase - 1];
            var instance = instanceValue.GetValueAsDreamObject();

            if (newProc is DreamProc dreamProc)
            {
                var currentFrame = CallStack.Pop();
                currentFrame.PC = pc;
                CallStack.Push(currentFrame);

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
                for (int i = 0; i < argCount; i++)
                {
                    arguments[i] = Stack[stackBase + i];
                }

                // Remove arguments and instance from stack
                Stack.RemoveRange(stackBase - 1, argCount + 1);

                var result = nativeProc.Call(this, instance, arguments);
                Push(result);
            }
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
                var cleanupStart = returnedFrame.StackBase;
                var cleanupCount = Stack.Count - cleanupStart;
                if (cleanupCount > 0)
                    Stack.RemoveRange(cleanupStart, cleanupCount);

                Push(returnValue);

                var newFrame = CallStack.Peek();
                proc = newFrame.Proc;
                pc = newFrame.PC;
            }
            else
            {
                Stack.Clear();
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
            var value = Stack[^1];
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
            var value = Stack[^1];
            var address = ReadInt32(proc, ref pc);
            if (value.Type == DreamValueType.Null)
                pc = address;
        }

        private void Opcode_BitAnd() => PerformBinaryOperation((a, b) => a & b);
        private void Opcode_BitOr() => PerformBinaryOperation((a, b) => a | b);
        private void Opcode_BitXor() => PerformBinaryOperation((a, b) => a ^ b);
        private void Opcode_BitNot() => Stack[^1] = ~Stack[^1];
        private void Opcode_BitShiftLeft() => PerformBinaryOperation((a, b) => a << b);
        private void Opcode_BitShiftRight() => PerformBinaryOperation((a, b) => a >> b);

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

        private void Opcode_Modulus() => PerformBinaryOperation((a, b) => new DreamValue((float)Math.IEEERemainder(a.AsFloat(), b.AsFloat())));

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
            var argCount = ReadByte(proc, ref pc);
            // In a real implementation, we would look up the proc on the object and call it.
            // For now, let's just pop arguments and push null.
            for (int i = 0; i < argCount; i++) Pop();
            Pop(); // Pop object
            Push(DreamValue.Null);
        }

        private void Opcode_Initial()
        {
            // Pops a variable reference and pushes its initial value.
            // Simplified: pop object and push null.
            Pop();
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

            var result = new System.Text.StringBuilder();
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

        private void Opcode_Power() => PerformBinaryOperation((a, b) => new DreamValue(MathF.Pow(a.AsFloat(), b.AsFloat())));
        private void Opcode_Sqrt() => Stack[^1] = new DreamValue(SharedOperations.Sqrt(Stack[^1].AsFloat()));
        private void Opcode_Abs() => Stack[^1] = new DreamValue(SharedOperations.Abs(Stack[^1].AsFloat()));
        private void Opcode_Sin() => Stack[^1] = new DreamValue(SharedOperations.Sin(Stack[^1].AsFloat()));
        private void Opcode_Cos() => Stack[^1] = new DreamValue(SharedOperations.Cos(Stack[^1].AsFloat()));
        private void Opcode_Tan() => Stack[^1] = new DreamValue(SharedOperations.Tan(Stack[^1].AsFloat()));
        private void Opcode_ArcSin() => Stack[^1] = new DreamValue(SharedOperations.ArcSin(Stack[^1].AsFloat()));
        private void Opcode_ArcCos() => Stack[^1] = new DreamValue(SharedOperations.ArcCos(Stack[^1].AsFloat()));
        private void Opcode_ArcTan() => Stack[^1] = new DreamValue(SharedOperations.ArcTan(Stack[^1].AsFloat()));
        private void Opcode_ArcTan2() => PerformBinaryOperation((x, y) => new DreamValue(SharedOperations.ArcTan(x.AsFloat(), y.AsFloat())));
        #endregion
    }
}
