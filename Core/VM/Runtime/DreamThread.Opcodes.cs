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

        private void Opcode_Add()
        {
            var b = Stack[^1];
            var a = Stack[^2];
            Stack.RemoveAt(Stack.Count - 1);
            Stack[^1] = a + b;
        }

        private void Opcode_Subtract()
        {
            var b = Stack[^1];
            var a = Stack[^2];
            Stack.RemoveAt(Stack.Count - 1);
            Stack[^1] = a - b;
        }

        private void Opcode_Multiply()
        {
            var b = Stack[^1];
            var a = Stack[^2];
            Stack.RemoveAt(Stack.Count - 1);
            Stack[^1] = a * b;
        }

        private void Opcode_Divide()
        {
            var b = Stack[^1];
            var a = Stack[^2];
            Stack.RemoveAt(Stack.Count - 1);
            Stack[^1] = a / b;
        }

        private void Opcode_CompareEquals()
        {
            var b = Stack[^1];
            var a = Stack[^2];
            Stack.RemoveAt(Stack.Count - 1);
            Stack[^1] = new DreamValue(a == b ? 1 : 0);
        }

        private void Opcode_CompareNotEquals()
        {
            var b = Stack[^1];
            var a = Stack[^2];
            Stack.RemoveAt(Stack.Count - 1);
            Stack[^1] = new DreamValue(a != b ? 1 : 0);
        }

        private void Opcode_CompareLessThan()
        {
            var b = Stack[^1];
            var a = Stack[^2];
            Stack.RemoveAt(Stack.Count - 1);
            Stack[^1] = new DreamValue(a < b ? 1 : 0);
        }

        private void Opcode_CompareGreaterThan()
        {
            var b = Stack[^1];
            var a = Stack[^2];
            Stack.RemoveAt(Stack.Count - 1);
            Stack[^1] = new DreamValue(a > b ? 1 : 0);
        }

        private void Opcode_CompareLessThanOrEqual()
        {
            var b = Stack[^1];
            var a = Stack[^2];
            Stack.RemoveAt(Stack.Count - 1);
            Stack[^1] = new DreamValue(a <= b ? 1 : 0);
        }

        private void Opcode_CompareGreaterThanOrEqual()
        {
            var b = Stack[^1];
            var a = Stack[^2];
            Stack.RemoveAt(Stack.Count - 1);
            Stack[^1] = new DreamValue(a >= b ? 1 : 0);
        }

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
            var newProc = _vm.Procs[procName];

            var stackBase = Stack.Count - argCount;

            var currentFrame = CallStack.Pop();
            currentFrame.PC = pc;
            CallStack.Push(currentFrame);

            var frame = new CallFrame(newProc, 0, stackBase);
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

        private void Opcode_BitAnd()
        {
            var b = Stack[^1];
            var a = Stack[^2];
            Stack.RemoveAt(Stack.Count - 1);
            Stack[^1] = a & b;
        }

        private void Opcode_BitOr()
        {
            var b = Stack[^1];
            var a = Stack[^2];
            Stack.RemoveAt(Stack.Count - 1);
            Stack[^1] = a | b;
        }

        private void Opcode_BitXor()
        {
            var b = Stack[^1];
            var a = Stack[^2];
            Stack.RemoveAt(Stack.Count - 1);
            Stack[^1] = a ^ b;
        }

        private void Opcode_BitNot()
        {
            Stack[^1] = ~Stack[^1];
        }

        private void Opcode_BitShiftLeft()
        {
            var b = Stack[^1];
            var a = Stack[^2];
            Stack.RemoveAt(Stack.Count - 1);
            Stack[^1] = a << b;
        }

        private void Opcode_BitShiftRight()
        {
            var b = Stack[^1];
            var a = Stack[^2];
            Stack.RemoveAt(Stack.Count - 1);
            Stack[^1] = a >> b;
        }
        #endregion
    }
}
