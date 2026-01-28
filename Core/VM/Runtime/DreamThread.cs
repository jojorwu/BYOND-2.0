using System;
using System.Collections.Generic;
using Core.VM.Procs;

using Shared;

namespace Core.VM.Runtime
{
    public partial class DreamThread : IScriptThread
    {
        private delegate void OpcodeHandler(DreamThread thread, ref DreamProc proc, CallFrame frame, ref int pc);
        private static readonly OpcodeHandler?[] _dispatchTable = new OpcodeHandler?[256];

        static DreamThread()
        {
            _dispatchTable[(byte)Opcode.PushString] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_PushString(p, ref pc);
            _dispatchTable[(byte)Opcode.PushFloat] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_PushFloat(p, ref pc);
            _dispatchTable[(byte)Opcode.Add] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Add();
            _dispatchTable[(byte)Opcode.Subtract] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Subtract();
            _dispatchTable[(byte)Opcode.Multiply] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Multiply();
            _dispatchTable[(byte)Opcode.Divide] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Divide();
            _dispatchTable[(byte)Opcode.CompareEquals] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_CompareEquals();
            _dispatchTable[(byte)Opcode.CompareNotEquals] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_CompareNotEquals();
            _dispatchTable[(byte)Opcode.CompareLessThan] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_CompareLessThan();
            _dispatchTable[(byte)Opcode.CompareGreaterThan] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_CompareGreaterThan();
            _dispatchTable[(byte)Opcode.CompareLessThanOrEqual] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_CompareLessThanOrEqual();
            _dispatchTable[(byte)Opcode.CompareGreaterThanOrEqual] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_CompareGreaterThanOrEqual();
            _dispatchTable[(byte)Opcode.Negate] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Negate();
            _dispatchTable[(byte)Opcode.BooleanNot] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_BooleanNot();
            _dispatchTable[(byte)Opcode.PushNull] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_PushNull();
            _dispatchTable[(byte)Opcode.Pop] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Pop();
            _dispatchTable[(byte)Opcode.Call] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Call(p, ref pc);
            _dispatchTable[(byte)Opcode.Jump] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Jump(p, ref pc);
            _dispatchTable[(byte)Opcode.JumpIfFalse] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_JumpIfFalse(p, ref pc);
            _dispatchTable[(byte)Opcode.Output] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Output();
            _dispatchTable[(byte)Opcode.Return] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Return(ref p, ref pc);
            _dispatchTable[(byte)Opcode.BitAnd] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_BitAnd();
            _dispatchTable[(byte)Opcode.BitOr] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_BitOr();
            _dispatchTable[(byte)Opcode.BitXor] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_BitXor();
            _dispatchTable[(byte)Opcode.BitNot] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_BitNot();
            _dispatchTable[(byte)Opcode.BitShiftLeft] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_BitShiftLeft();
            _dispatchTable[(byte)Opcode.BitShiftRight] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_BitShiftRight();
            _dispatchTable[(byte)Opcode.GetVariable] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_GetVariable(p, f, ref pc);
            _dispatchTable[(byte)Opcode.SetVariable] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_SetVariable(p, f, ref pc);
            _dispatchTable[(byte)Opcode.PushReferenceValue] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_PushReferenceValue(p, f, ref pc);
            _dispatchTable[(byte)Opcode.Assign] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Assign(p, f, ref pc);
            _dispatchTable[(byte)Opcode.PushGlobalVars] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_PushGlobalVars();
            _dispatchTable[(byte)Opcode.IsNull] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_IsNull();
            _dispatchTable[(byte)Opcode.JumpIfNull] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_JumpIfNull(p, ref pc);
            _dispatchTable[(byte)Opcode.JumpIfNullNoPop] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_JumpIfNullNoPop(p, ref pc);
            _dispatchTable[(byte)Opcode.BooleanAnd] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_BooleanAnd(p, ref pc);
            _dispatchTable[(byte)Opcode.BooleanOr] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_BooleanOr(p, ref pc);
            _dispatchTable[(byte)Opcode.Increment] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Increment(p, f, ref pc);
            _dispatchTable[(byte)Opcode.Decrement] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Decrement(p, f, ref pc);
            _dispatchTable[(byte)Opcode.Modulus] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Modulus();
            _dispatchTable[(byte)Opcode.AssignNoPush] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_AssignNoPush(p, f, ref pc);
            _dispatchTable[(byte)Opcode.CreateList] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_CreateList(p, ref pc);
            _dispatchTable[(byte)Opcode.IsInList] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_IsInList();
            _dispatchTable[(byte)Opcode.DereferenceField] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_DereferenceField(p, ref pc);
            _dispatchTable[(byte)Opcode.DereferenceIndex] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_DereferenceIndex();
            _dispatchTable[(byte)Opcode.DereferenceCall] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_DereferenceCall(p, ref pc);
            _dispatchTable[(byte)Opcode.Initial] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Initial();
            _dispatchTable[(byte)Opcode.IsType] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_IsType();
            _dispatchTable[(byte)Opcode.AsType] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_AsType();
            _dispatchTable[(byte)Opcode.CreateListEnumerator] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_CreateListEnumerator(p, ref pc);
            _dispatchTable[(byte)Opcode.Enumerate] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Enumerate(p, f, ref pc);
            _dispatchTable[(byte)Opcode.DestroyEnumerator] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_DestroyEnumerator(p, ref pc);
            _dispatchTable[(byte)Opcode.Append] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Append(p, f, ref pc);
            _dispatchTable[(byte)Opcode.Remove] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Remove(p, f, ref pc);
            _dispatchTable[(byte)Opcode.Prob] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Prob();
            _dispatchTable[(byte)Opcode.MassConcatenation] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_MassConcatenation(p, ref pc);
            _dispatchTable[(byte)Opcode.FormatString] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_FormatString(p, ref pc);
            _dispatchTable[(byte)Opcode.Power] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Power();
            _dispatchTable[(byte)Opcode.Sqrt] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Sqrt();
            _dispatchTable[(byte)Opcode.Abs] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Abs();
            _dispatchTable[(byte)Opcode.Sin] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Sin();
            _dispatchTable[(byte)Opcode.Cos] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Cos();
            _dispatchTable[(byte)Opcode.Tan] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_Tan();
            _dispatchTable[(byte)Opcode.ArcSin] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_ArcSin();
            _dispatchTable[(byte)Opcode.ArcCos] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_ArcCos();
            _dispatchTable[(byte)Opcode.ArcTan] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_ArcTan();
            _dispatchTable[(byte)Opcode.ArcTan2] = (DreamThread t, ref DreamProc p, CallFrame f, ref int pc) => t.Opcode_ArcTan2();
        }

        public List<DreamValue> Stack { get; } = new(128);
        public Stack<CallFrame> CallStack { get; } = new();
        public Dictionary<int, IEnumerator<DreamValue>> ActiveEnumerators { get; } = new();

        public DreamProc CurrentProc => CallStack.Peek().Proc;
        public DreamThreadState State { get; private set; } = DreamThreadState.Running;
        public IGameObject? AssociatedObject { get; }

        private readonly DreamVMContext _context;
        private readonly int _maxInstructions;
        private int _totalInstructionsExecuted;

        public DreamThread(DreamProc proc, DreamVMContext context, int maxInstructions, IGameObject? associatedObject = null)
        {
            _context = context;
            _maxInstructions = maxInstructions;
            AssociatedObject = associatedObject;

            CallStack.Push(new CallFrame(proc, 0, 0, null));
        }

        public void Push(DreamValue value)
        {
            Stack.Add(value);
        }

        public DreamValue Pop()
        {
            var value = Stack[^1];
            Stack.RemoveAt(Stack.Count - 1);
            return value;
        }

        private byte ReadByte(DreamProc proc, ref int pc)
        {
            if (pc + 1 > proc.Bytecode.Length)
                throw new Exception("Attempted to read past the end of the bytecode.");
            return proc.Bytecode[pc++];
        }

        private int ReadInt32(DreamProc proc, ref int pc)
        {
            if (pc + 4 > proc.Bytecode.Length)
                throw new Exception("Attempted to read past the end of the bytecode.");
            var value = BitConverter.ToInt32(proc.Bytecode, pc);
            pc += 4;
            return value;
        }

        private float ReadSingle(DreamProc proc, ref int pc)
        {
            if (pc + 4 > proc.Bytecode.Length)
                throw new Exception("Attempted to read past the end of the bytecode.");
            var value = BitConverter.ToSingle(proc.Bytecode, pc);
            pc += 4;
            return value;
        }

        private void SavePC(int pc)
        {
            if (CallStack.Count > 0)
            {
                var frame = CallStack.Pop();
                frame.PC = pc;
                CallStack.Push(frame);
            }
        }

        private DMReference ReadReference(DreamProc proc, ref int pc)
        {
            var refType = (DMReference.Type)ReadByte(proc, ref pc);
            switch (refType)
            {
                case DMReference.Type.Argument:
                case DMReference.Type.Local:
                    return new DMReference { RefType = refType, Index = ReadByte(proc, ref pc) };
                case DMReference.Type.Global:
                case DMReference.Type.GlobalProc:
                case DMReference.Type.Field:
                case DMReference.Type.SrcProc:
                case DMReference.Type.SrcField:
                    return new DMReference { RefType = refType, Index = ReadInt32(proc, ref pc) };
                default:
                    return new DMReference { RefType = refType };
            }
        }

        private DreamValue GetReferenceValue(DMReference reference, CallFrame frame)
        {
            switch (reference.RefType)
            {
                case DMReference.Type.Src:
                    return frame.Instance != null ? new DreamValue(frame.Instance) : DreamValue.Null;
                case DMReference.Type.Global:
                    return _context.Globals[reference.Index];
                case DMReference.Type.Argument:
                    return Stack[frame.StackBase + reference.Index];
                case DMReference.Type.Local:
                    return Stack[frame.StackBase + frame.Proc.Arguments.Length + reference.Index];
                case DMReference.Type.SrcField:
                    return frame.Instance != null ? frame.Instance.GetVariable(_context.Strings[reference.Index]) : DreamValue.Null;
                default:
                    throw new Exception($"Unsupported reference type for reading: {reference.RefType}");
            }
        }

        private void SetReferenceValue(DMReference reference, CallFrame frame, DreamValue value)
        {
            switch (reference.RefType)
            {
                case DMReference.Type.Global:
                    _context.Globals[reference.Index] = value;
                    break;
                case DMReference.Type.Argument:
                    Stack[frame.StackBase + reference.Index] = value;
                    break;
                case DMReference.Type.Local:
                    Stack[frame.StackBase + frame.Proc.Arguments.Length + reference.Index] = value;
                    break;
                case DMReference.Type.SrcField:
                    frame.Instance?.SetVariable(_context.Strings[reference.Index], value);
                    break;
                default:
                    throw new Exception($"Unsupported reference type for writing: {reference.RefType}");
            }
        }

        public DreamThreadState Run(int instructionBudget)
        {
            if (State != DreamThreadState.Running)
                return State;

            var instructionsExecutedThisTick = 0;

            var frame = CallStack.Peek();
            var proc = frame.Proc;
            var pc = frame.PC;

            while (State == DreamThreadState.Running)
            {
                if (instructionsExecutedThisTick++ >= instructionBudget)
                {
                    SavePC(pc);
                    return DreamThreadState.Running; // Budget exhausted, will resume next tick
                }

                if (_totalInstructionsExecuted++ > _maxInstructions)
                {
                    State = DreamThreadState.Error;
                    Console.WriteLine("Error: Instruction limit exceeded.");
                    SavePC(pc);
                    return State;
                }

                // If we've executed past the end of the bytecode, it's an implicit return.
                if (pc >= proc.Bytecode.Length)
                {
                    Push(DreamValue.Null);
                    Opcode_Return(ref proc, ref pc);
                    if(State == DreamThreadState.Running)
                        frame = CallStack.Peek();
                    continue;
                }

                try
                {
                    var opcode = (Opcode)ReadByte(proc, ref pc);
                    var handler = _dispatchTable[(byte)opcode];
                    if (handler != null)
                    {
                        handler(this, ref proc, frame, ref pc);
                        if (opcode == Opcode.Call || opcode == Opcode.Return)
                        {
                            if (State == DreamThreadState.Running)
                            {
                                frame = CallStack.Peek();
                                proc = frame.Proc;
                                pc = frame.PC;
                            }
                        }
                    }
                    else
                    {
                        State = DreamThreadState.Error;
                        throw new Exception($"Unknown opcode: {opcode}");
                    }
                }
                catch (Exception e)
                {
                    State = DreamThreadState.Error;
                    Console.WriteLine($"Error during script execution: {e.Message}");
                    SavePC(pc);
                    return State;
                }

                if (State != DreamThreadState.Running) // Check state after handler execution
                {
                    SavePC(pc);
                    return State;
                }
            }
            return State;
        }
    }
}
