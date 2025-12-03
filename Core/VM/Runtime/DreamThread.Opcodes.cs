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
            var b = Pop();
            var a = Pop();
            Push(operation(a, b));
        }
        private void Opcode_Add() => PerformBinaryOperation((a, b) => a + b);
        private void Opcode_Subtract() => PerformBinaryOperation((a, b) => a - b);
        private void Opcode_Multiply() => PerformBinaryOperation((a, b) => a * b);
        private void Opcode_Divide() => PerformBinaryOperation((a, b) => a / b);

        private void PerformComparisonOperation(Func<DreamValue, DreamValue, bool> operation)
        {
            var b = Pop();
            var a = Pop();
            Push(new DreamValue(operation(a, b) ? 1 : 0));
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

        private void Opcode_Call(ref CallFrame frame, DreamProc proc, ref int pc)
        {
            var procId = ReadInt32(proc, ref pc);
            var argCount = ReadByte(proc, ref pc);

            var procName = _vm.Strings[procId];
            var newProc = _vm.Procs[procName];

            var stackBase = Stack.Count - argCount;

            frame.PC = pc;
            CallStack.Pop(); // Pop the old frame, since we're modifying it
            CallStack.Push(frame); // Push the updated frame back

            frame = new CallFrame(newProc, 0, stackBase);
            CallStack.Push(frame);

            for (int i = 0; i < newProc.LocalVariableCount; i++)
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

        private void Opcode_Return(ref CallFrame frame, ref DreamProc proc, ref int pc)
        {
            var returnValue = Pop();

            CallStack.Pop(); // Pop the returning frame
            if (CallStack.Count > 0)
            {
                var cleanupStart = frame.StackBase;
                var cleanupCount = Stack.Count - cleanupStart;
                if (cleanupCount > 0)
                    Stack.RemoveRange(cleanupStart, cleanupCount);

                Push(returnValue);

                frame = CallStack.Peek();
                proc = frame.Proc;
                pc = frame.PC;
            }
            else
            {
                Stack.Clear();
                Push(returnValue);
                State = DreamThreadState.Finished;
            }
        }

        private void Opcode_PushArgument(DreamProc proc, CallFrame frame, ref int pc)
        {
            var argIndex = ReadByte(proc, ref pc);
            Push(Stack[frame.StackBase + argIndex]);
        }

        private void Opcode_SetArgument(DreamProc proc, CallFrame frame, ref int pc)
        {
            var argIndex = ReadByte(proc, ref pc);
            var value = Pop();
            Stack[frame.StackBase + argIndex] = value;
        }

        private void Opcode_PushLocal(DreamProc proc, CallFrame frame, ref int pc)
        {
            var localIndex = ReadByte(proc, ref pc);
            Push(Stack[frame.StackBase + frame.Proc.Arguments.Length + localIndex]);
        }

        private void Opcode_SetLocal(DreamProc proc, CallFrame frame, ref int pc)
        {
            var localIndex = ReadByte(proc, ref pc);
            var value = Pop();
            Stack[frame.StackBase + frame.Proc.Arguments.Length + localIndex] = value;
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
