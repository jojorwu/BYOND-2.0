using System;
using Core.VM.Opcodes;
using Core.VM.Procs;
using Core.VM.Types;

namespace Core.VM.Runtime
{
    public partial class DreamThread
    {
        #region Opcode Handlers
        private void Opcode_PushString(DreamProc proc, ref int pc)
        {
            var stringId = ReadInt32(proc, ref pc);
            Push(new DreamValue(_vm.Strings[stringId]));
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

            var procName = _vm.Strings[procId];
            if (!_vm.Procs.TryGetValue(procName, out var newProc) || newProc is not DreamProc dreamProc)
            {
                State = DreamThreadState.Error;
                throw new Exception($"Attempted to call non-existent proc: {procName}");
            }

            var stackBase = Stack.Count - argCount;
            var instanceValue = Stack[stackBase - 1];
            var instance = instanceValue.GetValueAsDreamObject();

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
            var variableName = _vm.Strings[variableNameId];

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
            var variableName = _vm.Strings[variableNameId];
            var value = Pop();

            var instance = frame.Instance;
            if (instance != null)
            {
                instance.SetVariable(variableName, value);
            }
        }

        private void Opcode_BitAnd() => PerformBinaryOperation((a, b) => a & b);
        private void Opcode_BitOr() => PerformBinaryOperation((a, b) => a | b);
        private void Opcode_BitXor() => PerformBinaryOperation((a, b) => a ^ b);
        private void Opcode_BitNot() => Stack[^1] = ~Stack[^1];
        private void Opcode_BitShiftLeft() => PerformBinaryOperation((a, b) => a << b);
        private void Opcode_BitShiftRight() => PerformBinaryOperation((a, b) => a >> b);
        #endregion
    }
}
