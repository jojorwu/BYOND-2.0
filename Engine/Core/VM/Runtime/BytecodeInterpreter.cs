using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Core.VM.Procs;
using Shared;

namespace Core.VM.Runtime
{
    public interface IBytecodeInterpreter
    {
        DreamThreadState Run(DreamThread thread, int instructionBudget);
    }

    internal unsafe ref struct InterpreterState
    {
        public DreamThread Thread;
        public CallFrame Frame;
        public DreamProc Proc;
        public int PC;
        public DreamValue[] Stack;
        public int StackPtr;
        public bool PotentiallyChangedStack;
        public ReadOnlySpan<byte> Bytecode;
        public int LocalBase;
        public int ArgumentBase;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(DreamValue value)
        {
            if (StackPtr >= DreamThread.MaxStackSize) throw new ScriptRuntimeException("Stack overflow", Proc, PC, Thread);
            if (StackPtr >= Stack.Length)
            {
                Thread._stackPtr = StackPtr;
                Thread.Push(value);
                Stack = Thread._stack;
                StackPtr = Thread._stackPtr;
            }
            else
            {
                Stack[StackPtr++] = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DreamValue Pop()
        {
            if (StackPtr <= 0) throw new ScriptRuntimeException("Stack underflow during Pop", Proc, PC, Thread);
            return Stack[--StackPtr];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
            return Bytecode[PC++];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt32()
        {
            var value = BinaryPrimitives.ReadInt32LittleEndian(Bytecode.Slice(PC));
            PC += 4;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadSingle()
        {
            var value = BinaryPrimitives.ReadSingleLittleEndian(Bytecode.Slice(PC));
            PC += 4;
            return value;
        }
    }

    public unsafe partial class BytecodeInterpreter : IBytecodeInterpreter
    {
        private static readonly delegate*<ref InterpreterState, void>[] _dispatchTable = CreateDispatchTable();

        public DreamThreadState Run(DreamThread thread, int instructionBudget)
        {
            if (thread.State != DreamThreadState.Running)
                return thread.State;

            var frame = thread._callStack[thread._callStackPtr - 1];
            var state = new InterpreterState
            {
                Thread = thread,
                Frame = frame,
                Proc = frame.Proc,
                PC = frame.PC,
                Stack = thread._stack,
                StackPtr = thread._stackPtr,
                Bytecode = frame.Proc.Bytecode,
                PotentiallyChangedStack = false,
                LocalBase = frame.StackBase + frame.Proc.Arguments.Length,
                ArgumentBase = frame.StackBase
            };

            int instructionsExecutedThisTick = 0;

            while (thread.State == DreamThreadState.Running)
            {
                if (instructionsExecutedThisTick++ >= instructionBudget) break;

                if (thread._totalInstructionsExecuted++ > thread._maxInstructions)
                {
                    thread.State = DreamThreadState.Error;
                    Console.WriteLine("Error: Total instruction limit exceeded for thread.");
                    break;
                }

                if (state.PC >= state.Bytecode.Length)
                {
                    thread._stackPtr = state.StackPtr;
                    thread.Push(DreamValue.Null);
                    thread.Opcode_Return(ref state.Proc, ref state.PC);
                    state.Stack = thread._stack;
                    state.StackPtr = thread._stackPtr;
                    if (thread.State == DreamThreadState.Running && thread._callStackPtr > 0)
                    {
                        state.Frame = thread._callStack[thread._callStackPtr - 1];
                        state.Proc = state.Frame.Proc;
                        state.PC = state.Frame.PC;
                        state.Bytecode = state.Proc.Bytecode;
                        state.LocalBase = state.Frame.StackBase + state.Proc.Arguments.Length;
                        state.ArgumentBase = state.Frame.StackBase;
                    }
                    continue;
                }

                try
                {
                    var opcode = (Opcode)state.Bytecode[state.PC++];
                    state.PotentiallyChangedStack = false;

                    _dispatchTable[(byte)opcode](ref state);

                    if (state.PotentiallyChangedStack)
                    {
                        if (thread.State == DreamThreadState.Running && thread._callStackPtr > 0)
                        {
                            state.Frame = thread._callStack[thread._callStackPtr - 1];
                            state.Proc = state.Frame.Proc;
                            state.PC = state.Frame.PC;
                            state.Bytecode = state.Proc.Bytecode;
                            state.Stack = thread._stack;
                            state.StackPtr = thread._stackPtr;
                            state.LocalBase = state.Frame.StackBase + state.Proc.Arguments.Length;
                            state.ArgumentBase = state.Frame.StackBase;
                        }
                    }
                }
                catch (Exception e) when (e is not ScriptRuntimeException)
                {
                    thread._stackPtr = state.StackPtr;
                    if (!thread.HandleException(new ScriptRuntimeException("Unexpected internal error", state.Proc, state.PC, thread, e)))
                        break;

                    if (thread._callStackPtr > 0)
                    {
                        state.Frame = thread._callStack[thread._callStackPtr - 1];
                        state.Proc = state.Frame.Proc;
                        state.PC = state.Frame.PC;
                        state.Bytecode = state.Proc.Bytecode;
                        state.Stack = thread._stack;
                        state.StackPtr = thread._stackPtr;
                        state.LocalBase = state.Frame.StackBase + state.Proc.Arguments.Length;
                        state.ArgumentBase = state.Frame.StackBase;
                    }
                }
                catch (ScriptRuntimeException e)
                {
                    thread._stackPtr = state.StackPtr;
                    if (!thread.HandleException(e))
                        break;

                    if (thread._callStackPtr > 0)
                    {
                        state.Frame = thread._callStack[thread._callStackPtr - 1];
                        state.Proc = state.Frame.Proc;
                        state.PC = state.Frame.PC;
                        state.Bytecode = state.Proc.Bytecode;
                        state.Stack = thread._stack;
                        state.StackPtr = thread._stackPtr;
                        state.LocalBase = state.Frame.StackBase + state.Proc.Arguments.Length;
                        state.ArgumentBase = state.Frame.StackBase;
                    }
                }
            }

            if (thread._callStackPtr > 0)
            {
                thread.SavePC(state.PC);
            }

            thread._stackPtr = state.StackPtr;
            return thread.State;
        }

        private static delegate*<ref InterpreterState, void>[] CreateDispatchTable()
        {
            var table = new delegate*<ref InterpreterState, void>[256];
            for (int i = 0; i < 256; i++) table[i] = &HandleUnknownOpcode;

            table[(byte)Opcode.PushString] = &HandlePushString;
            table[(byte)Opcode.PushFloat] = &HandlePushFloat;
            table[(byte)Opcode.PushNull] = &HandlePushNull;
            table[(byte)Opcode.Pop] = &HandlePop;
            table[(byte)Opcode.Add] = &HandleAdd;
            table[(byte)Opcode.Subtract] = &HandleSubtract;
            table[(byte)Opcode.Multiply] = &HandleMultiply;
            table[(byte)Opcode.Divide] = &HandleDivide;
            table[(byte)Opcode.CompareEquals] = &HandleCompareEquals;
            table[(byte)Opcode.CompareNotEquals] = &HandleCompareNotEquals;
            table[(byte)Opcode.CompareLessThan] = &HandleCompareLessThan;
            table[(byte)Opcode.CompareGreaterThan] = &HandleCompareGreaterThan;
            table[(byte)Opcode.CompareLessThanOrEqual] = &HandleCompareLessThanOrEqual;
            table[(byte)Opcode.CompareGreaterThanOrEqual] = &HandleCompareGreaterThanOrEqual;
            table[(byte)Opcode.CompareEquivalent] = &HandleCompareEquivalent;
            table[(byte)Opcode.CompareNotEquivalent] = &HandleCompareNotEquivalent;
            table[(byte)Opcode.Negate] = &HandleNegate;
            table[(byte)Opcode.BooleanNot] = &HandleBooleanNot;
            table[(byte)Opcode.Call] = &HandleCall;
            table[(byte)Opcode.CallStatement] = &HandleCallStatement;
            table[(byte)Opcode.PushProc] = &HandlePushProc;
            table[(byte)Opcode.Jump] = &HandleJump;
            table[(byte)Opcode.JumpIfFalse] = &HandleJumpIfFalse;
            table[(byte)Opcode.JumpIfTrueReference] = &HandleJumpIfTrueReference;
            table[(byte)Opcode.JumpIfFalseReference] = &HandleJumpIfFalseReference;
            table[(byte)Opcode.Output] = &HandleOutput;
            table[(byte)Opcode.OutputReference] = &HandleOutputReference;
            table[(byte)Opcode.Return] = &HandleReturn;
            table[(byte)Opcode.BitAnd] = &HandleBitAnd;
            table[(byte)Opcode.BitOr] = &HandleBitOr;
            table[(byte)Opcode.BitXor] = &HandleBitXor;
            table[(byte)Opcode.BitXorReference] = &HandleBitXorReference;
            table[(byte)Opcode.BitNot] = &HandleBitNot;
            table[(byte)Opcode.BitShiftLeft] = &HandleBitShiftLeft;
            table[(byte)Opcode.BitShiftLeftReference] = &HandleBitShiftLeftReference;
            table[(byte)Opcode.BitShiftRight] = &HandleBitShiftRight;
            table[(byte)Opcode.BitShiftRightReference] = &HandleBitShiftRightReference;
            table[(byte)Opcode.GetVariable] = &HandleGetVariable;
            table[(byte)Opcode.SetVariable] = &HandleSetVariable;
            table[(byte)Opcode.PushReferenceValue] = &HandlePushReferenceValue;
            table[(byte)Opcode.Assign] = &HandleAssign;
            table[(byte)Opcode.PushGlobalVars] = &HandlePushGlobalVars;
            table[(byte)Opcode.IsNull] = &HandleIsNull;
            table[(byte)Opcode.JumpIfNull] = &HandleJumpIfNull;
            table[(byte)Opcode.JumpIfNullNoPop] = &HandleJumpIfNullNoPop;
            table[(byte)Opcode.SwitchCase] = &HandleSwitchCase;
            table[(byte)Opcode.SwitchCaseRange] = &HandleSwitchCaseRange;
            table[(byte)Opcode.BooleanAnd] = &HandleBooleanAnd;
            table[(byte)Opcode.BooleanOr] = &HandleBooleanOr;
            table[(byte)Opcode.Increment] = &HandleIncrement;
            table[(byte)Opcode.Decrement] = &HandleDecrement;
            table[(byte)Opcode.Modulus] = &HandleModulus;
            table[(byte)Opcode.AssignInto] = &HandleAssignInto;
            table[(byte)Opcode.ModulusReference] = &HandleModulusReference;
            table[(byte)Opcode.ModulusModulus] = &HandleModulusModulus;
            table[(byte)Opcode.ModulusModulusReference] = &HandleModulusModulusReference;
            table[(byte)Opcode.CreateList] = &HandleCreateList;
            table[(byte)Opcode.CreateAssociativeList] = &HandleCreateAssociativeList;
            table[(byte)Opcode.CreateStrictAssociativeList] = &HandleCreateStrictAssociativeList;
            table[(byte)Opcode.IsInList] = &HandleIsInList;
            table[(byte)Opcode.Input] = &HandleInput;
            table[(byte)Opcode.PickUnweighted] = &HandlePickUnweighted;
            table[(byte)Opcode.PickWeighted] = &HandlePickWeighted;
            table[(byte)Opcode.DereferenceField] = &HandleDereferenceField;
            table[(byte)Opcode.DereferenceIndex] = &HandleDereferenceIndex;
            table[(byte)Opcode.PopReference] = &HandlePopReference;
            table[(byte)Opcode.DereferenceCall] = &HandleDereferenceCall;
            table[(byte)Opcode.Initial] = &HandleInitial;
            table[(byte)Opcode.IsType] = &HandleIsType;
            table[(byte)Opcode.AsType] = &HandleAsType;
            table[(byte)Opcode.CreateListEnumerator] = &HandleCreateListEnumerator;
            table[(byte)Opcode.Enumerate] = &HandleEnumerate;
            table[(byte)Opcode.EnumerateAssoc] = &HandleEnumerateAssoc;
            table[(byte)Opcode.DestroyEnumerator] = &HandleDestroyEnumerator;
            table[(byte)Opcode.Append] = &HandleAppend;
            table[(byte)Opcode.Remove] = &HandleRemove;
            table[(byte)Opcode.DeleteObject] = &HandleDeleteObject;
            table[(byte)Opcode.Prob] = &HandleProb;
            table[(byte)Opcode.IsSaved] = &HandleIsSaved;
            table[(byte)Opcode.GetStep] = &HandleGetStep;
            table[(byte)Opcode.GetStepTo] = &HandleGetStepTo;
            table[(byte)Opcode.GetDist] = &HandleGetDist;
            table[(byte)Opcode.GetDir] = &HandleGetDir;
            table[(byte)Opcode.MassConcatenation] = &HandleMassConcatenation;
            table[(byte)Opcode.FormatString] = &HandleFormatString;
            table[(byte)Opcode.Power] = &HandlePower;
            table[(byte)Opcode.Sqrt] = &HandleSqrt;
            table[(byte)Opcode.Abs] = &HandleAbs;
            table[(byte)Opcode.MultiplyReference] = &HandleMultiplyReference;
            table[(byte)Opcode.Sin] = &HandleSin;
            table[(byte)Opcode.DivideReference] = &HandleDivideReference;
            table[(byte)Opcode.Cos] = &HandleCos;
            table[(byte)Opcode.Tan] = &HandleTan;
            table[(byte)Opcode.ArcSin] = &HandleArcSin;
            table[(byte)Opcode.ArcCos] = &HandleArcCos;
            table[(byte)Opcode.ArcTan] = &HandleArcTan;
            table[(byte)Opcode.ArcTan2] = &HandleArcTan2;
            table[(byte)Opcode.Log] = &HandleLog;
            table[(byte)Opcode.LogE] = &HandleLogE;
            table[(byte)Opcode.PushType] = &HandlePushType;
            table[(byte)Opcode.CreateObject] = &HandleCreateObject;
            table[(byte)Opcode.LocateCoord] = &HandleLocateCoord;
            table[(byte)Opcode.Locate] = &HandleLocate;
            table[(byte)Opcode.Length] = &HandleLength;
            table[(byte)Opcode.IsInRange] = &HandleIsInRange;
            table[(byte)Opcode.Throw] = &HandleThrow;
            table[(byte)Opcode.Try] = &HandleTry;
            table[(byte)Opcode.TryNoValue] = &HandleTryNoValue;
            table[(byte)Opcode.EndTry] = &HandleEndTry;
            table[(byte)Opcode.Spawn] = &HandleSpawn;
            table[(byte)Opcode.Rgb] = &HandleRgb;
            table[(byte)Opcode.Gradient] = &HandleGradient;
            table[(byte)Opcode.AppendNoPush] = &HandleAppendNoPush;
            table[(byte)Opcode.AssignNoPush] = &HandleAssignNoPush;
            table[(byte)Opcode.PushRefAndDereferenceField] = &HandlePushRefAndDereferenceField;
            table[(byte)Opcode.PushNRefs] = &HandlePushNRefs;
            table[(byte)Opcode.PushNFloats] = &HandlePushNFloats;
            table[(byte)Opcode.PushStringFloat] = &HandlePushStringFloat;
            table[(byte)Opcode.PushResource] = &HandlePushResource;
            table[(byte)Opcode.SwitchOnFloat] = &HandleSwitchOnFloat;
            table[(byte)Opcode.SwitchOnString] = &HandleSwitchOnString;
            table[(byte)Opcode.JumpIfReferenceFalse] = &HandleJumpIfReferenceFalse;
            table[(byte)Opcode.ReturnFloat] = &HandleReturnFloat;
            table[(byte)Opcode.NPushFloatAssign] = &HandleNPushFloatAssign;
            table[(byte)Opcode.IsTypeDirect] = &HandleIsTypeDirect;
            table[(byte)Opcode.NullRef] = &HandleNullRef;
            table[(byte)Opcode.IndexRefWithString] = &HandleIndexRefWithString;
            table[(byte)Opcode.ReturnReferenceValue] = &HandleReturnReferenceValue;
            table[(byte)Opcode.PushFloatAssign] = &HandlePushFloatAssign;
            table[(byte)Opcode.PushLocal] = &HandlePushLocal;
            table[(byte)Opcode.AssignLocal] = &HandleAssignLocal;
            table[(byte)Opcode.PushArgument] = &HandlePushArgument;
            table[(byte)Opcode.LocalPushLocalPushAdd] = &HandleLocalPushLocalPushAdd;
            table[(byte)Opcode.LocalAddFloat] = &HandleLocalAddFloat;
            table[(byte)Opcode.LocalMulAdd] = &HandleLocalMulAdd;

            return table;
        }

        private static void HandleUnknownOpcode(ref InterpreterState state)
        {
            throw new ScriptRuntimeException($"Unknown opcode: 0x{(byte)state.Bytecode[state.PC - 1]:X2}", state.Proc, state.PC - 1, state.Thread);
        }

        private static void HandlePushString(ref InterpreterState state)
        {
            var stringId = state.ReadInt32();
            if (stringId < 0 || stringId >= state.Thread.Context.Strings.Count)
                throw new ScriptRuntimeException($"Invalid string ID: {stringId}", state.Proc, state.PC, state.Thread);
            state.Push(new DreamValue(state.Thread.Context.Strings[stringId]));
        }

        private static void HandlePushFloat(ref InterpreterState state)
        {
            state.Push(new DreamValue(state.ReadSingle()));
        }

        private static void HandlePushNull(ref InterpreterState state)
        {
            state.Push(DreamValue.Null);
        }

        private static void HandlePop(ref InterpreterState state)
        {
            state.StackPtr--;
        }

        private static void HandleAdd(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Add", state.Proc, state.PC, state.Thread);
            var b = state.Stack[--state.StackPtr];
            var a = state.Stack[state.StackPtr - 1];
            if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                state.Stack[state.StackPtr - 1] = new DreamValue(a.RawFloat + b.RawFloat);
            else
                state.Stack[state.StackPtr - 1] = a + b;
        }

        private static void HandleSubtract(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Subtract", state.Proc, state.PC, state.Thread);
            var b = state.Stack[--state.StackPtr];
            var a = state.Stack[state.StackPtr - 1];
            if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                state.Stack[state.StackPtr - 1] = new DreamValue(a.RawFloat - b.RawFloat);
            else
                state.Stack[state.StackPtr - 1] = a - b;
        }

        private static void HandleMultiply(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Multiply", state.Proc, state.PC, state.Thread);
            var b = state.Stack[--state.StackPtr];
            var a = state.Stack[state.StackPtr - 1];
            if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                state.Stack[state.StackPtr - 1] = new DreamValue(a.RawFloat * b.RawFloat);
            else
                state.Stack[state.StackPtr - 1] = a * b;
        }

        private static void HandleDivide(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Divide", state.Proc, state.PC, state.Thread);
            var b = state.Stack[--state.StackPtr];
            var a = state.Stack[state.StackPtr - 1];
            if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
            {
                var fb = b.RawFloat;
                state.Stack[state.StackPtr - 1] = new DreamValue(fb != 0 ? a.RawFloat / fb : 0);
            }
            else
                state.Stack[state.StackPtr - 1] = a / b;
        }

        private static void HandleCompareEquals(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareEquals", state.Proc, state.PC, state.Thread);
            var b = state.Stack[--state.StackPtr];
            state.Stack[state.StackPtr - 1] = state.Stack[state.StackPtr - 1] == b ? DreamValue.True : DreamValue.False;
        }

        private static void HandleCompareNotEquals(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareNotEquals", state.Proc, state.PC, state.Thread);
            var b = state.Stack[--state.StackPtr];
            state.Stack[state.StackPtr - 1] = state.Stack[state.StackPtr - 1] != b ? DreamValue.True : DreamValue.False;
        }

        private static void HandleCompareLessThan(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareLessThan", state.Proc, state.PC, state.Thread);
            var b = state.Stack[--state.StackPtr];
            state.Stack[state.StackPtr - 1] = state.Stack[state.StackPtr - 1] < b ? DreamValue.True : DreamValue.False;
        }

        private static void HandleCompareGreaterThan(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareGreaterThan", state.Proc, state.PC, state.Thread);
            var b = state.Stack[--state.StackPtr];
            state.Stack[state.StackPtr - 1] = state.Stack[state.StackPtr - 1] > b ? DreamValue.True : DreamValue.False;
        }

        private static void HandleCompareLessThanOrEqual(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareLessThanOrEqual", state.Proc, state.PC, state.Thread);
            var b = state.Stack[--state.StackPtr];
            state.Stack[state.StackPtr - 1] = state.Stack[state.StackPtr - 1] <= b ? DreamValue.True : DreamValue.False;
        }

        private static void HandleCompareGreaterThanOrEqual(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareGreaterThanOrEqual", state.Proc, state.PC, state.Thread);
            var b = state.Stack[--state.StackPtr];
            state.Stack[state.StackPtr - 1] = state.Stack[state.StackPtr - 1] >= b ? DreamValue.True : DreamValue.False;
        }

        private static void HandleCompareEquivalent(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareEquivalent", state.Proc, state.PC, state.Thread);
            var b = state.Stack[--state.StackPtr];
            state.Stack[state.StackPtr - 1] = state.Stack[state.StackPtr - 1].Equals(b) ? DreamValue.True : DreamValue.False;
        }

        private static void HandleCompareNotEquivalent(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareNotEquivalent", state.Proc, state.PC, state.Thread);
            var b = state.Stack[--state.StackPtr];
            state.Stack[state.StackPtr - 1] = !state.Stack[state.StackPtr - 1].Equals(b) ? DreamValue.True : DreamValue.False;
        }

        private static void HandleNegate(ref InterpreterState state)
        {
            if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during Negate", state.Proc, state.PC, state.Thread);
            state.Stack[state.StackPtr - 1] = -state.Stack[state.StackPtr - 1];
        }

        private static void HandleBooleanNot(ref InterpreterState state)
        {
            if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during BooleanNot", state.Proc, state.PC, state.Thread);
            state.Stack[state.StackPtr - 1] = state.Stack[state.StackPtr - 1].IsFalse() ? DreamValue.True : DreamValue.False;
        }

        private static void HandleCall(ref InterpreterState state)
        {
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            var argType = (DMCallArgumentsType)state.ReadByte();
            var argStackDelta = state.ReadInt32();
            var unusedStackDelta = state.ReadInt32();

            state.Thread.SavePC(state.PC);
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.PerformCall(reference, argType, argStackDelta);
            state.PotentiallyChangedStack = true;
        }

        private static void HandleCallStatement(ref InterpreterState state)
        {
            var argType = (DMCallArgumentsType)state.ReadByte();
            var argStackDelta = state.ReadInt32();

            if (argStackDelta < 0 || state.StackPtr < argStackDelta)
                throw new ScriptRuntimeException($"Invalid argument stack delta for ..() call: {argStackDelta}", state.Proc, state.PC, state.Thread);

            var instance = state.Frame.Instance;
            IDreamProc? parentProc = null;

            if (instance != null && instance.ObjectType != null)
            {
                ObjectType? definingType = null;
                ObjectType? current = instance.ObjectType;
                while (current != null)
                {
                    if (current.Procs.ContainsValue(state.Proc))
                    {
                        definingType = current;
                        break;
                    }
                    current = current.Parent;
                }

                if (definingType != null)
                {
                    parentProc = definingType.Parent?.GetProc(state.Proc.Name);
                }
            }

            if (parentProc != null)
            {
                state.Thread.SavePC(state.PC);
                state.Thread._stackPtr = state.StackPtr;
                state.Thread.PerformCall(parentProc, instance, argStackDelta, argStackDelta);
            }
            else
            {
                state.StackPtr -= argStackDelta;
                state.Push(DreamValue.Null);
            }
            state.PotentiallyChangedStack = true;
        }

        private static void HandlePushProc(ref InterpreterState state)
        {
            var procId = state.ReadInt32();
            DreamValue val;
            if (procId >= 0 && procId < state.Thread.Context.AllProcs.Count)
                val = new DreamValue((IDreamProc)state.Thread.Context.AllProcs[procId]);
            else
                val = DreamValue.Null;
            state.Push(val);
        }

        private static void HandleJump(ref InterpreterState state)
        {
            state.PC = state.ReadInt32();
        }

        private static void HandleJumpIfFalse(ref InterpreterState state)
        {
            if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during JumpIfFalse", state.Proc, state.PC, state.Thread);
            var val = state.Stack[--state.StackPtr];
            var address = state.ReadInt32();
            if (val.IsFalse()) state.PC = address;
        }

        private static void HandleJumpIfTrueReference(ref InterpreterState state)
        {
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            var address = state.ReadInt32();
            state.Thread._stackPtr = state.StackPtr;
            var val = state.Thread.GetReferenceValue(reference, state.Frame, 0);
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.StackPtr = state.Thread._stackPtr;
            if (!val.IsFalse()) state.PC = address;
        }

        private static void HandleJumpIfFalseReference(ref InterpreterState state)
        {
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            var address = state.ReadInt32();
            state.Thread._stackPtr = state.StackPtr;
            var val = state.Thread.GetReferenceValue(reference, state.Frame, 0);
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.StackPtr = state.Thread._stackPtr;
            if (val.IsFalse()) state.PC = address;
        }

        private static void HandleOutput(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Output", state.Proc, state.PC, state.Thread);
            var message = state.Stack[--state.StackPtr];
            var target = state.Stack[--state.StackPtr];
            if (!message.IsNull) Console.WriteLine(message.ToString());
        }

        private static void HandleOutputReference(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_OutputReference(state.Proc, state.Frame, ref state.PC);
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleReturn(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_Return(ref state.Proc, ref state.PC);
            state.Stack = state.Thread._stack;
            state.StackPtr = state.Thread._stackPtr;
            state.PotentiallyChangedStack = true;
        }

        private static void HandleBitAnd(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during BitAnd", state.Proc, state.PC, state.Thread);
            var b = state.Stack[--state.StackPtr];
            state.Stack[state.StackPtr - 1] &= b;
        }

        private static void HandleBitOr(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during BitOr", state.Proc, state.PC, state.Thread);
            var b = state.Stack[--state.StackPtr];
            state.Stack[state.StackPtr - 1] |= b;
        }

        private static void HandleBitXor(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during BitXor", state.Proc, state.PC, state.Thread);
            var b = state.Stack[--state.StackPtr];
            state.Stack[state.StackPtr - 1] ^= b;
        }

        private static void HandleBitXorReference(ref InterpreterState state)
        {
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            var value = state.Stack[--state.StackPtr];
            state.Thread._stackPtr = state.StackPtr;
            var refValue = state.Thread.GetReferenceValue(reference, state.Frame, 0);
            state.Thread.SetReferenceValue(reference, state.Frame, refValue ^ value, 0);
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleBitNot(ref InterpreterState state)
        {
            if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during BitNot", state.Proc, state.PC, state.Thread);
            state.Stack[state.StackPtr - 1] = ~state.Stack[state.StackPtr - 1];
        }

        private static void HandleBitShiftLeft(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during BitShiftLeft", state.Proc, state.PC, state.Thread);
            var b = state.Stack[--state.StackPtr];
            state.Stack[state.StackPtr - 1] <<= b;
        }

        private static void HandleBitShiftLeftReference(ref InterpreterState state)
        {
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            var value = state.Stack[--state.StackPtr];
            state.Thread._stackPtr = state.StackPtr;
            var refValue = state.Thread.GetReferenceValue(reference, state.Frame, 0);
            state.Thread.SetReferenceValue(reference, state.Frame, refValue << value, 0);
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleBitShiftRight(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during BitShiftRight", state.Proc, state.PC, state.Thread);
            var b = state.Stack[--state.StackPtr];
            state.Stack[state.StackPtr - 1] >>= b;
        }

        private static void HandleBitShiftRightReference(ref InterpreterState state)
        {
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            var value = state.Stack[--state.StackPtr];
            state.Thread._stackPtr = state.StackPtr;
            var refValue = state.Thread.GetReferenceValue(reference, state.Frame, 0);
            state.Thread.SetReferenceValue(reference, state.Frame, refValue >> value, 0);
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleGetVariable(ref InterpreterState state)
        {
            var nameId = state.ReadInt32();
            var instance = state.Frame.Instance;
            DreamValue val = DreamValue.Null;
            if (instance != null)
            {
                var name = state.Thread.Context.Strings[nameId];
                int idx = instance.ObjectType?.GetVariableIndex(name) ?? -1;
                val = idx != -1 ? instance.GetVariableDirect(idx) : instance.GetVariable(name);
            }
            state.Push(val);
        }

        private static void HandleSetVariable(ref InterpreterState state)
        {
            var nameId = state.ReadInt32();
            var val = state.Stack[--state.StackPtr];
            if (state.Frame.Instance != null)
            {
                var name = state.Thread.Context.Strings[nameId];
                int idx = state.Frame.Instance.ObjectType?.GetVariableIndex(name) ?? -1;
                if (idx != -1) state.Frame.Instance.SetVariableDirect(idx, val);
                else state.Frame.Instance.SetVariable(name, val);
            }
        }

        private static void HandlePushReferenceValue(ref InterpreterState state)
        {
            var refType = (DMReference.Type)state.Bytecode[state.PC++];
            switch (refType)
            {
                case DMReference.Type.Local:
                    {
                        int idx = state.Bytecode[state.PC++];
                        if (idx < 0 || idx >= state.Proc.LocalVariableCount) throw new ScriptRuntimeException("Local index out of bounds", state.Proc, state.PC, state.Thread);
                        state.Push(state.Stack[state.LocalBase + idx]);
                    }
                    break;
                case DMReference.Type.Argument:
                    {
                        int idx = state.Bytecode[state.PC++];
                        if (idx < 0 || idx >= state.Proc.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", state.Proc, state.PC, state.Thread);
                        state.Push(state.Stack[state.ArgumentBase + idx]);
                    }
                    break;
                case DMReference.Type.Global:
                    state.Push(state.Thread.Context.GetGlobal(state.ReadInt32()));
                    break;
                case DMReference.Type.Src:
                    state.Push(state.Frame.Instance != null ? new DreamValue(state.Frame.Instance) : DreamValue.Null);
                    break;
                case DMReference.Type.World:
                    state.Push(state.Thread.Context.World != null ? new DreamValue(state.Thread.Context.World) : DreamValue.Null);
                    break;
                default:
                    {
                        state.PC--;
                        var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
                        state.Thread._stackPtr = state.StackPtr;
                        var val = state.Thread.GetReferenceValue(reference, state.Frame, 0);
                        state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                        state.StackPtr = state.Thread._stackPtr;
                        state.Push(val);
                    }
                    break;
            }
        }

        private static void HandleAssign(ref InterpreterState state)
        {
            var refType = (DMReference.Type)state.Bytecode[state.PC++];
            var value = state.Stack[state.StackPtr - 1]; // peek
            switch (refType)
            {
                case DMReference.Type.Local:
                    {
                        int idx = state.Bytecode[state.PC++];
                        if (idx < 0 || idx >= state.Proc.LocalVariableCount) throw new ScriptRuntimeException("Local index out of bounds", state.Proc, state.PC, state.Thread);
                        state.Stack[state.LocalBase + idx] = value;
                    }
                    break;
                case DMReference.Type.Argument:
                    {
                        int idx = state.Bytecode[state.PC++];
                        if (idx < 0 || idx >= state.Proc.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", state.Proc, state.PC, state.Thread);
                        state.Stack[state.ArgumentBase + idx] = value;
                    }
                    break;
                case DMReference.Type.Global:
                    state.Thread.Context.SetGlobal(state.ReadInt32(), value);
                    break;
                default:
                    {
                        state.PC--;
                        var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
                        var val = state.Pop();
                        state.Thread._stackPtr = state.StackPtr;
                        state.Thread.SetReferenceValue(reference, state.Frame, val, 0);
                        state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                        state.StackPtr = state.Thread._stackPtr;
                        state.Push(val);
                    }
                    break;
            }
        }

        private static void HandlePushGlobalVars(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_PushGlobalVars();
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleIsNull(ref InterpreterState state)
        {
            state.Stack[state.StackPtr - 1] = state.Stack[state.StackPtr - 1].IsNull ? DreamValue.True : DreamValue.False;
        }

        private static void HandleJumpIfNull(ref InterpreterState state)
        {
            if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during JumpIfNull", state.Proc, state.PC, state.Thread);
            var val = state.Stack[--state.StackPtr];
            var address = state.ReadInt32();
            if (val.Type == DreamValueType.Null) state.PC = address;
        }

        private static void HandleJumpIfNullNoPop(ref InterpreterState state)
        {
            if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during JumpIfNullNoPop", state.Proc, state.PC, state.Thread);
            var val = state.Stack[state.StackPtr - 1];
            var address = state.ReadInt32();
            if (val.Type == DreamValueType.Null) state.PC = address;
        }

        private static void HandleSwitchCase(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during SwitchCase", state.Proc, state.PC, state.Thread);
            var caseValue = state.Stack[--state.StackPtr];
            var switchValue = state.Stack[state.StackPtr - 1];
            var jumpAddress = state.ReadInt32();
            if (switchValue == caseValue) state.PC = jumpAddress;
        }

        private static void HandleSwitchCaseRange(ref InterpreterState state)
        {
            if (state.StackPtr < 3) throw new ScriptRuntimeException("Stack underflow during SwitchCaseRange", state.Proc, state.PC, state.Thread);
            var max = state.Stack[--state.StackPtr];
            var min = state.Stack[--state.StackPtr];
            var switchValue = state.Stack[state.StackPtr - 1];
            var jumpAddress = state.ReadInt32();
            if (switchValue >= min && switchValue <= max) state.PC = jumpAddress;
        }

        private static void HandleBooleanAnd(ref InterpreterState state)
        {
            if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during BooleanAnd", state.Proc, state.PC, state.Thread);
            var val = state.Stack[--state.StackPtr];
            var jumpAddress = state.ReadInt32();
            if (val.IsFalse())
            {
                state.Push(val);
                state.PC = jumpAddress;
            }
        }

        private static void HandleBooleanOr(ref InterpreterState state)
        {
            if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during BooleanOr", state.Proc, state.PC, state.Thread);
            var val = state.Stack[--state.StackPtr];
            var jumpAddress = state.ReadInt32();
            if (!val.IsFalse())
            {
                state.Push(val);
                state.PC = jumpAddress;
            }
        }

        private static void HandleIncrement(ref InterpreterState state)
        {
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            state.Thread._stackPtr = state.StackPtr;
            var value = state.Thread.GetReferenceValue(reference, state.Frame, 0);
            var newValue = value + 1;
            state.Thread.SetReferenceValue(reference, state.Frame, newValue, 0);
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.StackPtr = state.Thread._stackPtr;
            state.Push(newValue);
        }

        private static void HandleDecrement(ref InterpreterState state)
        {
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            state.Thread._stackPtr = state.StackPtr;
            var value = state.Thread.GetReferenceValue(reference, state.Frame, 0);
            var newValue = value - 1;
            state.Thread.SetReferenceValue(reference, state.Frame, newValue, 0);
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.StackPtr = state.Thread._stackPtr;
            state.Push(newValue);
        }

        private static void HandleModulus(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_Modulus();
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleAssignInto(ref InterpreterState state)
        {
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            var value = state.Stack[--state.StackPtr];
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.SetReferenceValue(reference, state.Frame, value, 0);
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.Thread.Push(value);
            state.Stack = state.Thread._stack;
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleModulusReference(ref InterpreterState state)
        {
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            var value = state.Stack[--state.StackPtr];
            state.Thread._stackPtr = state.StackPtr;
            var refValue = state.Thread.GetReferenceValue(reference, state.Frame, 0);
            state.Thread.SetReferenceValue(reference, state.Frame, refValue % value, 0);
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleModulusModulus(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_ModulusModulus();
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleModulusModulusReference(ref InterpreterState state)
        {
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            var value = state.Stack[--state.StackPtr];
            state.Thread._stackPtr = state.StackPtr;
            var refValue = state.Thread.GetReferenceValue(reference, state.Frame, 0);
            state.Thread.SetReferenceValue(reference, state.Frame, new DreamValue(SharedOperations.Modulo(refValue.GetValueAsFloat(), value.GetValueAsFloat())), 0);
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleCreateList(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_CreateList(state.Proc, ref state.PC);
            state.Stack = state.Thread._stack;
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleCreateAssociativeList(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_CreateAssociativeList(state.Proc, ref state.PC);
            state.Stack = state.Thread._stack;
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleCreateStrictAssociativeList(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_CreateStrictAssociativeList(state.Proc, ref state.PC);
            state.Stack = state.Thread._stack;
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleIsInList(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_IsInList();
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleInput(ref InterpreterState state)
        {
            var ref1 = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            var ref2 = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            state.StackPtr -= 4;
            state.Push(DreamValue.Null);
        }

        private static void HandlePickUnweighted(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_PickUnweighted(state.Proc, ref state.PC);
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandlePickWeighted(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_PickWeighted(state.Proc, ref state.PC);
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleDereferenceField(ref InterpreterState state)
        {
            if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during DereferenceField", state.Proc, state.PC, state.Thread);
            var nameId = state.ReadInt32();
            var objValue = state.Stack[--state.StackPtr];
            DreamValue val = DreamValue.Null;
            if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
            {
                var name = state.Thread.Context.Strings[nameId];
                int idx = obj.ObjectType?.GetVariableIndex(name) ?? -1;
                val = idx != -1 ? obj.GetVariableDirect(idx) : obj.GetVariable(name);
            }
            state.Push(val);
        }

        private static void HandleDereferenceIndex(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during DereferenceIndex", state.Proc, state.PC, state.Thread);
            var index = state.Stack[--state.StackPtr];
            var objValue = state.Stack[--state.StackPtr];
            DreamValue val = DreamValue.Null;
            if (objValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
            {
                if (index.Type == DreamValueType.Float)
                {
                    int i = (int)index.RawFloat - 1;
                    if (i >= 0 && i < list.Values.Count) val = list.Values[i];
                }
                else val = list.GetValue(index);
            }
            state.Push(val);
        }

        private static void HandlePopReference(ref InterpreterState state)
        {
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleDereferenceCall(ref InterpreterState state)
        {
            var nameId = state.ReadInt32();
            var argType = (DMCallArgumentsType)state.ReadByte();
            var argStackDelta = state.ReadInt32();

            if (argStackDelta < 1 || state.StackPtr < argStackDelta)
                throw new ScriptRuntimeException($"Invalid argument stack delta for dereference call: {argStackDelta}", state.Proc, state.PC, state.Thread);

            var objValue = state.Stack[state.StackPtr - argStackDelta];
            if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
            {
                var procName = state.Thread.Context.Strings[nameId];
                var targetProc = obj.ObjectType?.GetProc(procName);
                if (targetProc == null)
                {
                    var varValue = obj.GetVariable(procName);
                    if (varValue.TryGetValue(out IDreamProc? procFromVar)) targetProc = procFromVar;
                }

                if (targetProc != null)
                {
                    state.Thread.SavePC(state.PC);
                    int argCount = argStackDelta - 1;
                    int stackBase = state.StackPtr - argStackDelta;
                    for (int i = 0; i < argCount; i++) state.Stack[stackBase + i] = state.Stack[stackBase + i + 1];
                    state.StackPtr--;
                    state.Thread._stackPtr = state.StackPtr;
                    state.Thread.PerformCall(targetProc, obj, argCount, argCount);
                    state.PotentiallyChangedStack = true;
                    return;
                }
            }
            state.StackPtr -= argStackDelta;
            state.Push(DreamValue.Null);
            state.PotentiallyChangedStack = true;
        }

        private static void HandleInitial(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Initial", state.Proc, state.PC, state.Thread);
            var key = state.Stack[--state.StackPtr];
            var objValue = state.Stack[--state.StackPtr];
            DreamValue result = DreamValue.Null;
            if (objValue.TryGetValue(out DreamObject? obj) && obj != null && obj.ObjectType != null)
            {
                if (key.TryGetValue(out string? varName) && varName != null)
                {
                    int index = obj.ObjectType.GetVariableIndex(varName);
                    if (index != -1 && index < obj.ObjectType.FlattenedDefaultValues.Count)
                        result = DreamValue.FromObject(obj.ObjectType.FlattenedDefaultValues[index]);
                }
            }
            state.Push(result);
        }

        private static void HandleIsType(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during IsType", state.Proc, state.PC, state.Thread);
            var typeValue = state.Stack[--state.StackPtr];
            var objValue = state.Stack[state.StackPtr - 1];
            bool result = false;
            if (objValue.Type == DreamValueType.DreamObject && typeValue.Type == DreamValueType.DreamType)
            {
                var obj = objValue.GetValueAsDreamObject();
                typeValue.TryGetValue(out ObjectType? type);
                if (obj?.ObjectType != null && type != null) result = obj.ObjectType.IsSubtypeOf(type);
            }
            state.Stack[state.StackPtr - 1] = result ? DreamValue.True : DreamValue.False;
        }

        private static void HandleAsType(ref InterpreterState state)
        {
            if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during AsType", state.Proc, state.PC, state.Thread);
            var typeValue = state.Stack[--state.StackPtr];
            var objValue = state.Stack[state.StackPtr - 1];
            bool matches = false;
            if (objValue.Type == DreamValueType.DreamObject && typeValue.Type == DreamValueType.DreamType)
            {
                var obj = objValue.GetValueAsDreamObject();
                typeValue.TryGetValue(out ObjectType? type);
                if (obj?.ObjectType != null && type != null) matches = obj.ObjectType.IsSubtypeOf(type);
            }
            state.Stack[state.StackPtr - 1] = matches ? objValue : DreamValue.Null;
        }

        private static void HandleCreateListEnumerator(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_CreateListEnumerator(state.Proc, ref state.PC);
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleEnumerate(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_Enumerate(state.Proc, state.Frame, ref state.PC);
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleEnumerateAssoc(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_EnumerateAssoc(state.Proc, state.Frame, ref state.PC);
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleDestroyEnumerator(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_DestroyEnumerator(state.Proc, ref state.PC);
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleAppend(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_Append(state.Proc, state.Frame, ref state.PC);
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleRemove(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_Remove(state.Proc, state.Frame, ref state.PC);
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleDeleteObject(ref InterpreterState state)
        {
            var value = state.Stack[--state.StackPtr];
            if (value.TryGetValueAsGameObject(out var obj)) state.Thread.Context.GameState?.RemoveGameObject(obj);
        }

        private static void HandleProb(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_Prob();
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleIsSaved(ref InterpreterState state)
        {
            state.Push(DreamValue.True);
        }

        private static void HandleGetStep(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_GetStep();
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleGetStepTo(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_GetStepTo();
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleGetDist(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_GetDist();
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleGetDir(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_GetDir();
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleMassConcatenation(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_MassConcatenation(state.Proc, ref state.PC);
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleFormatString(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_FormatString(state.Proc, ref state.PC);
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandlePower(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_Power();
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleSqrt(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_Sqrt();
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleAbs(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_Abs();
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleMultiplyReference(ref InterpreterState state)
        {
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            var value = state.Stack[--state.StackPtr];
            state.Thread._stackPtr = state.StackPtr;
            var refValue = state.Thread.GetReferenceValue(reference, state.Frame, 0);
            state.Thread.SetReferenceValue(reference, state.Frame, refValue * value, 0);
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleSin(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_Sin();
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleDivideReference(ref InterpreterState state)
        {
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            var value = state.Stack[--state.StackPtr];
            state.Thread._stackPtr = state.StackPtr;
            var refValue = state.Thread.GetReferenceValue(reference, state.Frame, 0);
            state.Thread.SetReferenceValue(reference, state.Frame, refValue / value, 0);
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleCos(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_Cos();
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleTan(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_Tan();
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleArcSin(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_ArcSin();
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleArcCos(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_ArcCos();
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleArcTan(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_ArcTan();
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleArcTan2(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_ArcTan2();
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleLog(ref InterpreterState state)
        {
            var baseValue = state.Stack[--state.StackPtr];
            var x = state.Stack[--state.StackPtr];
            state.Push(new DreamValue(MathF.Log(x.GetValueAsFloat(), baseValue.GetValueAsFloat())));
        }

        private static void HandleLogE(ref InterpreterState state)
        {
            state.Push(new DreamValue(MathF.Log(state.Stack[--state.StackPtr].GetValueAsFloat())));
        }

        private static void HandlePushType(ref InterpreterState state)
        {
            var typeId = state.ReadInt32();
            var type = state.Thread.Context.ObjectTypeManager?.GetObjectType(typeId);
            state.Push(type != null ? new DreamValue(type) : DreamValue.Null);
        }

        private static void HandleCreateObject(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_CreateObject(state.Proc, ref state.PC);
            state.Stack = state.Thread._stack;
            state.StackPtr = state.Thread._stackPtr;
            state.PotentiallyChangedStack = true;
        }

        private static void HandleLocateCoord(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_LocateCoord();
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleLocate(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_Locate();
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleLength(ref InterpreterState state)
        {
            var value = state.Stack[--state.StackPtr];
            DreamValue result;
            if (value.Type == DreamValueType.String && value.TryGetValue(out string? str)) result = new DreamValue(str?.Length ?? 0);
            else if (value.Type == DreamValueType.DreamObject && value.TryGetValue(out DreamObject? obj) && obj is DreamList list) result = new DreamValue(list.Values.Count);
            else result = new DreamValue(0);
            state.Push(result);
        }

        private static void HandleIsInRange(ref InterpreterState state)
        {
            if (state.StackPtr < 3) throw new ScriptRuntimeException("Stack underflow during IsInRange", state.Proc, state.PC, state.Thread);
            var max = state.Stack[--state.StackPtr];
            var min = state.Stack[--state.StackPtr];
            var val = state.Stack[--state.StackPtr];
            state.Push(new DreamValue(val >= min && val <= max ? 1 : 0));
        }

        private static void HandleThrow(ref InterpreterState state)
        {
            var value = state.Stack[--state.StackPtr];
            var e = new ScriptRuntimeException(value.ToString(), state.Proc, state.PC, state.Thread) { ThrownValue = value };
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.HandleException(e);
            state.Stack = state.Thread._stack;
            state.StackPtr = state.Thread._stackPtr;
            state.PotentiallyChangedStack = true;
        }

        private static void HandleTry(ref InterpreterState state)
        {
            var catchAddress = state.ReadInt32();
            var catchRef = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            state.Thread.TryStack.Push(new TryBlock { CatchAddress = catchAddress, CallStackDepth = state.Thread.CallStackCount, StackPointer = state.StackPtr, CatchReference = catchRef });
        }

        private static void HandleTryNoValue(ref InterpreterState state)
        {
            var catchAddress = state.ReadInt32();
            state.Thread.TryStack.Push(new TryBlock { CatchAddress = catchAddress, CallStackDepth = state.Thread.CallStackCount, StackPointer = state.StackPtr, CatchReference = null });
        }

        private static void HandleEndTry(ref InterpreterState state)
        {
            if (state.Thread.TryStack.Count > 0) state.Thread.TryStack.Pop();
        }

        private static void HandleSpawn(ref InterpreterState state)
        {
            if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during Spawn", state.Proc, state.PC, state.Thread);
            var address = state.ReadInt32();
            var bodyPc = state.PC;
            state.PC = address;
            var delay = state.Stack[--state.StackPtr];
            state.Thread._stackPtr = state.StackPtr;
            var newThread = new DreamThread(state.Thread, bodyPc);
            if (delay.TryGetValue(out float seconds) && seconds > 0) newThread.Sleep(seconds / 10.0f);
            state.Thread.Context.ScriptHost?.AddThread(newThread);
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleRgb(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_Rgb(state.Proc, ref state.PC);
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleGradient(ref InterpreterState state)
        {
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_Gradient(state.Proc, ref state.PC);
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleAppendNoPush(ref InterpreterState state)
        {
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            var value = state.Pop();
            state.Thread._stackPtr = state.StackPtr;
            var listValue = state.Thread.GetReferenceValue(reference, state.Frame);
            if (listValue.Type == DreamValueType.DreamObject && listValue.TryGetValue(out DreamObject? obj) && obj is DreamList list) list.AddValue(value);
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleAssignNoPush(ref InterpreterState state)
        {
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            var value = state.Pop();
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.SetReferenceValue(reference, state.Frame, value);
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandlePushRefAndDereferenceField(ref InterpreterState state)
        {
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            var fieldNameId = state.ReadInt32();
            var fieldName = state.Thread.Context.Strings[fieldNameId];
            state.Thread._stackPtr = state.StackPtr;
            var objValue = state.Thread.GetReferenceValue(reference, state.Frame);
            DreamValue val = DreamValue.Null;
            if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj != null) val = obj.GetVariable(fieldName);
            state.StackPtr = state.Thread._stackPtr;
            state.Push(val);
        }

        private static void HandlePushNRefs(ref InterpreterState state)
        {
            var count = state.ReadInt32();
            state.Thread._stackPtr = state.StackPtr;
            for (int i = 0; i < count; i++)
            {
                var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
                var val = state.Thread.GetReferenceValue(reference, state.Frame);
                state.Thread.Push(val);
            }
            state.Stack = state.Thread._stack;
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandlePushNFloats(ref InterpreterState state)
        {
            var count = state.ReadInt32();
            for (int i = 0; i < count; i++) state.Push(new DreamValue(state.ReadSingle()));
        }

        private static void HandlePushStringFloat(ref InterpreterState state)
        {
            var stringId = state.ReadInt32();
            var value = state.ReadSingle();
            state.Push(new DreamValue(state.Thread.Context.Strings[stringId]));
            state.Push(new DreamValue(value));
        }

        private static void HandlePushResource(ref InterpreterState state)
        {
            var pathId = state.ReadInt32();
            state.Push(new DreamValue(new DreamResource("resource", state.Thread.Context.Strings[pathId])));
        }

        private static void HandleSwitchOnFloat(ref InterpreterState state)
        {
            if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during SwitchOnFloat", state.Proc, state.PC, state.Thread);
            var value = state.ReadSingle();
            var jumpAddress = state.ReadInt32();
            var switchValue = state.Stack[state.StackPtr - 1];
            if (switchValue.Type == DreamValueType.Float && switchValue.RawFloat == value) state.PC = jumpAddress;
        }

        private static void HandleSwitchOnString(ref InterpreterState state)
        {
            if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during SwitchOnString", state.Proc, state.PC, state.Thread);
            var stringId = state.ReadInt32();
            var jumpAddress = state.ReadInt32();
            var switchValue = state.Stack[state.StackPtr - 1];
            if (switchValue.Type == DreamValueType.String && switchValue.TryGetValue(out string? s) && s == state.Thread.Context.Strings[stringId]) state.PC = jumpAddress;
        }

        private static void HandleJumpIfReferenceFalse(ref InterpreterState state)
        {
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            var address = state.ReadInt32();
            state.Thread._stackPtr = state.StackPtr;
            var val = state.Thread.GetReferenceValue(reference, state.Frame, 0);
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.StackPtr = state.Thread._stackPtr;
            if (val.IsFalse()) state.PC = address;
        }

        private static void HandleReturnFloat(ref InterpreterState state)
        {
            state.Push(new DreamValue(state.ReadSingle()));
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.Opcode_Return(ref state.Proc, ref state.PC);
            state.Stack = state.Thread._stack;
            state.StackPtr = state.Thread._stackPtr;
            state.PotentiallyChangedStack = true;
        }

        private static void HandleNPushFloatAssign(ref InterpreterState state)
        {
            int n = state.ReadInt32();
            float value = state.ReadSingle();
            var dv = new DreamValue(value);
            state.Thread._stackPtr = state.StackPtr;
            for (int i = 0; i < n; i++)
            {
                var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
                state.Thread.SetReferenceValue(reference, state.Frame, dv, 0);
                state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            }
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleIsTypeDirect(ref InterpreterState state)
        {
            int typeId = state.ReadInt32();
            var value = state.Stack[--state.StackPtr];
            bool result = false;
            if (value.Type == DreamValueType.DreamObject && value.TryGetValue(out DreamObject? obj) && obj != null)
            {
                var ot = obj.ObjectType;
                if (ot != null)
                {
                    var targetType = state.Thread.Context.ObjectTypeManager?.GetObjectType(typeId);
                    if (targetType != null) result = ot.IsSubtypeOf(targetType);
                }
            }
            state.Push(result ? DreamValue.True : DreamValue.False);
        }

        private static void HandleNullRef(ref InterpreterState state)
        {
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.SetReferenceValue(reference, state.Frame, DreamValue.Null, 0);
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleIndexRefWithString(ref InterpreterState state)
        {
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            var stringId = state.ReadInt32();
            var stringValue = new DreamValue(state.Thread.Context.Strings[stringId]);
            state.Thread._stackPtr = state.StackPtr;
            var objValue = state.Thread.GetReferenceValue(reference, state.Frame, 0);
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            DreamValue result = DreamValue.Null;
            if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj is DreamList list) result = list.GetValue(stringValue);
            state.Push(result);
            state.Stack = state.Thread._stack;
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandleReturnReferenceValue(ref InterpreterState state)
        {
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            state.Thread._stackPtr = state.StackPtr;
            var val = state.Thread.GetReferenceValue(reference, state.Frame, 0);
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.Thread.Push(val);
            state.Thread.Opcode_Return(ref state.Proc, ref state.PC);
            state.Stack = state.Thread._stack;
            state.StackPtr = state.Thread._stackPtr;
            state.PotentiallyChangedStack = true;
        }

        private static void HandlePushFloatAssign(ref InterpreterState state)
        {
            var value = state.ReadSingle();
            var reference = state.Thread.ReadReference(state.Bytecode, ref state.PC);
            var dv = new DreamValue(value);
            state.Thread._stackPtr = state.StackPtr;
            state.Thread.SetReferenceValue(reference, state.Frame, dv, 0);
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.Thread.Push(dv);
            state.Stack = state.Thread._stack;
            state.StackPtr = state.Thread._stackPtr;
        }

        private static void HandlePushLocal(ref InterpreterState state)
        {
            int idx = state.ReadByte();
            if (idx < 0 || idx >= state.Proc.LocalVariableCount) throw new ScriptRuntimeException("Local index out of bounds", state.Proc, state.PC, state.Thread);
            state.Push(state.Stack[state.LocalBase + idx]);
        }

        private static void HandleAssignLocal(ref InterpreterState state)
        {
            int idx = state.ReadByte();
            if (idx < 0 || idx >= state.Proc.LocalVariableCount) throw new ScriptRuntimeException("Local index out of bounds", state.Proc, state.PC, state.Thread);
            state.Stack[state.LocalBase + idx] = state.Stack[state.StackPtr - 1];
        }

        private static void HandlePushArgument(ref InterpreterState state)
        {
            int idx = state.ReadByte();
            if (idx < 0 || idx >= state.Proc.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", state.Proc, state.PC, state.Thread);
            state.Push(state.Stack[state.ArgumentBase + idx]);
        }

        private static void HandleLocalPushLocalPushAdd(ref InterpreterState state)
        {
            int idx1 = state.ReadByte();
            int idx2 = state.ReadByte();
            if (idx1 < 0 || idx1 >= state.Proc.LocalVariableCount || idx2 < 0 || idx2 >= state.Proc.LocalVariableCount)
                throw new ScriptRuntimeException("Local index out of bounds", state.Proc, state.PC, state.Thread);

            var a = state.Stack[state.LocalBase + idx1];
            var b = state.Stack[state.LocalBase + idx2];

            if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float)
                state.Push(new DreamValue(a.RawFloat + b.RawFloat));
            else
                state.Push(a + b);
        }

        private static void HandleLocalAddFloat(ref InterpreterState state)
        {
            int idx = state.ReadByte();
            float val = state.ReadSingle();
            if (idx < 0 || idx >= state.Proc.LocalVariableCount) throw new ScriptRuntimeException("Local index out of bounds", state.Proc, state.PC, state.Thread);

            var a = state.Stack[state.LocalBase + idx];
            if (a.Type == DreamValueType.Float)
                state.Push(new DreamValue(a.RawFloat + val));
            else
                state.Push(a + val);
        }

        private static void HandleLocalMulAdd(ref InterpreterState state)
        {
            int idx1 = state.ReadByte();
            int idx2 = state.ReadByte();
            int idx3 = state.ReadByte();
            if (idx1 < 0 || idx1 >= state.Proc.LocalVariableCount || idx2 < 0 || idx2 >= state.Proc.LocalVariableCount || idx3 < 0 || idx3 >= state.Proc.LocalVariableCount)
                throw new ScriptRuntimeException("Local index out of bounds", state.Proc, state.PC, state.Thread);

            var a = state.Stack[state.LocalBase + idx1];
            var b = state.Stack[state.LocalBase + idx2];
            var c = state.Stack[state.LocalBase + idx3];

            if (a.Type == DreamValueType.Float && b.Type == DreamValueType.Float && c.Type == DreamValueType.Float)
                state.Push(new DreamValue(a.RawFloat * b.RawFloat + c.RawFloat));
            else
                state.Push(a * b + c);
        }
    }
}
