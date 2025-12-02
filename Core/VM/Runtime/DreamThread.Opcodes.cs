using System;
using Core.VM.Types;

namespace Core.VM.Runtime
{
    public partial class DreamThread
    {
        #region Opcode Handlers
        private void Opcode_PushString()
        {
            var stringId = ReadInt32();
            Push(new DreamValue(_vm.Strings[stringId]));
        }

        private void Opcode_PushFloat()
        {
            var value = ReadSingle();
            Push(new DreamValue(value));
        }

        private void Opcode_Add()
        {
            var b = Pop();
            var a = Pop();
            Push(a + b);
        }

        private void Opcode_Subtract()
        {
            var b = Pop();
            var a = Pop();
            Push(a - b);
        }

        private void Opcode_Multiply()
        {
            var b = Pop();
            var a = Pop();
            Push(a * b);
        }

        private void Opcode_Divide()
        {
            var b = Pop();
            var a = Pop();
            Push(a / b);
        }

        private void Opcode_CompareEquals()
        {
            var b = Pop();
            var a = Pop();
            Push(new DreamValue(a == b ? 1 : 0));
        }

        private void Opcode_CompareNotEquals()
        {
            var b = Pop();
            var a = Pop();
            Push(new DreamValue(a != b ? 1 : 0));
        }

        private void Opcode_CompareLessThan()
        {
            var b = Pop();
            var a = Pop();
            Push(new DreamValue(a < b ? 1 : 0));
        }

        private void Opcode_CompareGreaterThan()
        {
            var b = Pop();
            var a = Pop();
            Push(new DreamValue(a > b ? 1 : 0));
        }

        private void Opcode_CompareLessThanOrEqual()
        {
            var b = Pop();
            var a = Pop();
            Push(new DreamValue(a <= b ? 1 : 0));
        }

        private void Opcode_CompareGreaterThanOrEqual()
        {
            var b = Pop();
            var a = Pop();
            Push(new DreamValue(a >= b ? 1 : 0));
        }

        private void Opcode_Negate()
        {
            var value = Pop();
            Push(-value);
        }

        private void Opcode_BooleanNot()
        {
            var value = Pop();
            Push(!value);
        }

        private void Opcode_PushNull()
        {
            Push(DreamValue.Null);
        }

        private void Opcode_Pop()
        {
            Pop();
        }

        private void Opcode_Initial()
        {
            var propertyName = _vm.Strings[ReadInt32()];
            DreamObject? dreamObject;
            Pop().TryGetValue(out dreamObject);
            if (dreamObject == null)
                throw new Exception("Cannot get initial value of non-object.");

            var objectType = dreamObject.GameObject.ObjectType;
            if (objectType.DefaultProperties.TryGetValue(propertyName, out var value))
            {
                if (value is float f)
                    Push(new DreamValue(f));
                else if (value is string s)
                    Push(new DreamValue(s));
                else
                    throw new Exception($"Unsupported initial value type: {value.GetType()}");
            }
            else
            {
                Push(DreamValue.Null);
            }
        }

        private void Opcode_Call()
        {
            var procId = ReadInt32();
            var argCount = ReadByte(); // Read the number of arguments from the bytecode

            var procName = _vm.Strings[procId];
            var proc = _vm.Procs[procName];

            // The arguments are the last `argCount` items on the stack.
            // The base of the new stack frame will be where the arguments start.
            var stackBase = Stack.Count - argCount;

            var frame = new CallFrame(proc, PC, stackBase);
            CallStack.Push(frame);

            // Allocate space for local variables by pushing nulls.
            for (int i = 0; i < proc.LocalVariableCount; i++)
            {
                Push(DreamValue.Null);
            }
        }

        private void Opcode_Jump()
        {
            var address = ReadInt32();
            PC = address;
        }

        private void Opcode_JumpIfFalse()
        {
            var value = Pop();
            var address = ReadInt32();
            if (value.IsFalse())
                PC = address;
        }

        private void Opcode_Output()
        {
            var value = Pop();
            Console.WriteLine(value.ToString());
        }

        private void Opcode_Return()
        {
            // The return value is on top of the stack.
            var returnValue = Pop();

            var returnedFrame = CallStack.Pop();
            if (CallStack.Count > 0)
            {
                // The frame's data starts at StackBase. We need to remove everything from there to the current top.
                var cleanupStart = returnedFrame.StackBase;
                var cleanupCount = Stack.Count - cleanupStart;
                if (cleanupCount > 0)
                    Stack.RemoveRange(cleanupStart, cleanupCount);

                // Push the return value back onto the cleaned stack.
                Push(returnValue);

                // Set the PC of the *new* top frame to the return address we stored.
                PC = returnedFrame.ReturnAddress;
            }
            else
            {
                // We've returned from the last proc, thread is finished.
                // Clear the stack and push the final return value.
                Stack.Clear();
                Push(returnValue);
                State = DreamThreadState.Finished;
            }
        }

        private void Opcode_PushArgument()
        {
            var argIndex = ReadByte();
            var frame = CallStack.Peek();
            Push(Stack[frame.StackBase + argIndex]);
        }

        private void Opcode_SetArgument()
        {
            var argIndex = ReadByte();
            var value = Pop();
            var frame = CallStack.Peek();
            Stack[frame.StackBase + argIndex] = value;
        }

        private void Opcode_PushLocal()
        {
            var localIndex = ReadByte();
            var frame = CallStack.Peek();
            Push(Stack[frame.StackBase + frame.Proc.Arguments.Length + localIndex]);
        }

        private void Opcode_SetLocal()
        {
            var localIndex = ReadByte();
            var value = Pop();
            var frame = CallStack.Peek();
            Stack[frame.StackBase + frame.Proc.Arguments.Length + localIndex] = value;
        }

        private void Opcode_BitAnd()
        {
            var b = Pop();
            var a = Pop();
            Push(a & b);
        }

        private void Opcode_BitOr()
        {
            var b = Pop();
            var a = Pop();
            Push(a | b);
        }

        private void Opcode_BitXor()
        {
            var b = Pop();
            var a = Pop();
            Push(a ^ b);
        }

        private void Opcode_BitNot()
        {
            var value = Pop();
            Push(~value);
        }

        private void Opcode_BitShiftLeft()
        {
            var b = Pop();
            var a = Pop();
            Push(a << b);
        }

        private void Opcode_BitShiftRight()
        {
            var b = Pop();
            var a = Pop();
            Push(a >> b);
        }

        private void Opcode_CompareEquivalent()
        {
            var b = Pop();
            var a = Pop();
            Push(new DreamValue(a.IsEquivalent(b) ? 1.0f : 0.0f));
        }

        private void Opcode_CompareNotEquivalent()
        {
            var b = Pop();
            var a = Pop();
            Push(new DreamValue(!a.IsEquivalent(b) ? 1.0f : 0.0f));
        }
        #endregion
    }
}
