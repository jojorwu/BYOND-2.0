using System;
using Shared;
using Core.VM.Procs;
using Core.VM.Types;

namespace Core.VM.Runtime
{
    public partial class DreamThread
    {
        private delegate void OpcodeHandler(DreamThread thread, ref DreamProc proc, ref int pc);
        private static readonly OpcodeHandler[] _dispatchTable = new OpcodeHandler[256];

        static DreamThread()
        {
            _dispatchTable[(byte)Opcode.PushString] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_PushString(ref p, ref pc);
            _dispatchTable[(byte)Opcode.PushFloat] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_PushFloat(ref p, ref pc);
            _dispatchTable[(byte)Opcode.Add] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_Add(ref p, ref pc);
            _dispatchTable[(byte)Opcode.Subtract] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_Subtract(ref p, ref pc);
            _dispatchTable[(byte)Opcode.Multiply] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_Multiply(ref p, ref pc);
            _dispatchTable[(byte)Opcode.Divide] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_Divide(ref p, ref pc);
            _dispatchTable[(byte)Opcode.CompareEquals] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_CompareEquals(ref p, ref pc);
            _dispatchTable[(byte)Opcode.CompareNotEquals] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_CompareNotEquals(ref p, ref pc);
            _dispatchTable[(byte)Opcode.CompareLessThan] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_CompareLessThan(ref p, ref pc);
            _dispatchTable[(byte)Opcode.CompareGreaterThan] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_CompareGreaterThan(ref p, ref pc);
            _dispatchTable[(byte)Opcode.CompareLessThanOrEqual] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_CompareLessThanOrEqual(ref p, ref pc);
            _dispatchTable[(byte)Opcode.CompareGreaterThanOrEqual] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_CompareGreaterThanOrEqual(ref p, ref pc);
            _dispatchTable[(byte)Opcode.Negate] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_Negate(ref p, ref pc);
            _dispatchTable[(byte)Opcode.BooleanNot] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_BooleanNot(ref p, ref pc);
            _dispatchTable[(byte)Opcode.PushNull] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_PushNull(ref p, ref pc);
            _dispatchTable[(byte)Opcode.Pop] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_Pop(ref p, ref pc);
            _dispatchTable[(byte)Opcode.Call] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_Call(ref p, ref pc);
            _dispatchTable[(byte)Opcode.Jump] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_Jump(ref p, ref pc);
            _dispatchTable[(byte)Opcode.JumpIfFalse] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_JumpIfFalse(ref p, ref pc);
            _dispatchTable[(byte)Opcode.Output] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_Output(ref p, ref pc);
            _dispatchTable[(byte)Opcode.Return] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_Return(ref p, ref pc);
            _dispatchTable[(byte)Opcode.BitAnd] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_BitAnd(ref p, ref pc);
            _dispatchTable[(byte)Opcode.BitOr] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_BitOr(ref p, ref pc);
            _dispatchTable[(byte)Opcode.BitXor] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_BitXor(ref p, ref pc);
            _dispatchTable[(byte)Opcode.BitNot] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_BitNot(ref p, ref pc);
            _dispatchTable[(byte)Opcode.BitShiftLeft] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_BitShiftLeft(ref p, ref pc);
            _dispatchTable[(byte)Opcode.BitShiftRight] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_BitShiftRight(ref p, ref pc);
            _dispatchTable[(byte)Opcode.GetVariable] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_GetVariable(ref p, ref pc);
            _dispatchTable[(byte)Opcode.SetVariable] = (DreamThread t, ref DreamProc p, ref int pc) => t.Opcode_SetVariable(ref p, ref pc);
        }

        #region Opcode Handlers
        private void Opcode_PushString(ref DreamProc proc, ref int pc)
        {
            var stringId = ReadInt32(proc, ref pc);
            Push(new DreamValue(_context.Strings[stringId]));
        }

        private void Opcode_PushFloat(ref DreamProc proc, ref int pc)
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
        private void Opcode_Add(ref DreamProc proc, ref int pc) => PerformBinaryOperation((a, b) => a + b);
        private void Opcode_Subtract(ref DreamProc proc, ref int pc) => PerformBinaryOperation((a, b) => a - b);
        private void Opcode_Multiply(ref DreamProc proc, ref int pc) => PerformBinaryOperation((a, b) => a * b);
        private void Opcode_Divide(ref DreamProc proc, ref int pc) => PerformBinaryOperation((a, b) => a / b);

        private void PerformComparisonOperation(Func<DreamValue, DreamValue, bool> operation)
        {
            var b = Stack[^1];
            var a = Stack[^2];
            Stack.RemoveAt(Stack.Count - 1);
            Stack[^1] = new DreamValue(operation(a, b) ? 1 : 0);
        }

        private void Opcode_CompareEquals(ref DreamProc proc, ref int pc) => PerformComparisonOperation((a, b) => a == b);
        private void Opcode_CompareNotEquals(ref DreamProc proc, ref int pc) => PerformComparisonOperation((a, b) => a != b);
        private void Opcode_CompareLessThan(ref DreamProc proc, ref int pc) => PerformComparisonOperation((a, b) => a < b);
        private void Opcode_CompareGreaterThan(ref DreamProc proc, ref int pc) => PerformComparisonOperation((a, b) => a > b);
        private void Opcode_CompareLessThanOrEqual(ref DreamProc proc, ref int pc) => PerformComparisonOperation((a, b) => a <= b);
        private void Opcode_CompareGreaterThanOrEqual(ref DreamProc proc, ref int pc) => PerformComparisonOperation((a, b) => a >= b);

        private void Opcode_Negate(ref DreamProc proc, ref int pc)
        {
            Stack[^1] = -Stack[^1];
        }

        private void Opcode_BooleanNot(ref DreamProc proc, ref int pc)
        {
            Stack[^1] = !Stack[^1];
        }

        private void Opcode_PushNull(ref DreamProc proc, ref int pc)
        {
            Push(DreamValue.Null);
        }

        private void Opcode_Pop(ref DreamProc proc, ref int pc)
        {
            Pop();
        }

        private void Opcode_Call(ref DreamProc proc, ref int pc)
        {
            var procId = ReadInt32(proc, ref pc);
            var argCount = ReadByte(proc, ref pc);

            var procName = _context.Strings[procId];
            if (!_context.Procs.TryGetValue(procName, out var newProc) || newProc is not DreamProc dreamProc)
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

            proc = dreamProc;
            pc = 0;
        }

        private void Opcode_Jump(ref DreamProc proc, ref int pc)
        {
            var address = ReadInt32(proc, ref pc);
            pc = address;
        }

        private void Opcode_JumpIfFalse(ref DreamProc proc, ref int pc)
        {
            var value = Pop();
            var address = ReadInt32(proc, ref pc);
            if (value.IsFalse())
                pc = address;
        }

        private void Opcode_Output(ref DreamProc proc, ref int pc)
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

        private void Opcode_GetVariable(ref DreamProc proc, ref int pc)
        {
            var variableNameId = ReadInt32(proc, ref pc);
            var variableName = _context.Strings[variableNameId];

            var frame = CallStack.Peek();
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

        private void Opcode_SetVariable(ref DreamProc proc, ref int pc)
        {
            var variableNameId = ReadInt32(proc, ref pc);
            var variableName = _context.Strings[variableNameId];
            var value = Pop();

            var frame = CallStack.Peek();
            var instance = frame.Instance;
            if (instance != null)
            {
                instance.SetVariable(variableName, value);
            }
        }

        private void Opcode_BitAnd(ref DreamProc proc, ref int pc) => PerformBinaryOperation((a, b) => a & b);
        private void Opcode_BitOr(ref DreamProc proc, ref int pc) => PerformBinaryOperation((a, b) => a | b);
        private void Opcode_BitXor(ref DreamProc proc, ref int pc) => PerformBinaryOperation((a, b) => a ^ b);
        private void Opcode_BitNot(ref DreamProc proc, ref int pc) => Stack[^1] = ~Stack[^1];
        private void Opcode_BitShiftLeft(ref DreamProc proc, ref int pc) => PerformBinaryOperation((a, b) => a << b);
        private void Opcode_BitShiftRight(ref DreamProc proc, ref int pc) => PerformBinaryOperation((a, b) => a >> b);
        #endregion
    }
}
