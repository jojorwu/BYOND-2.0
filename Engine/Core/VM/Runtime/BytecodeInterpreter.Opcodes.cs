using Shared.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Core.VM.Procs;
using Shared;

namespace Core.VM.Runtime;

public unsafe partial class BytecodeInterpreter
{
    private static readonly delegate*<ref InterpreterState, void>[] _dispatchTable = CreateDispatchTable();

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
        table[(byte)Opcode.GetBuiltinVar] = &HandleGetBuiltinVar;
        table[(byte)Opcode.SetBuiltinVar] = &HandleSetBuiltinVar;
        table[(byte)Opcode.LocalPushReturn] = &HandleLocalPushReturn;
        table[(byte)Opcode.LocalCompareEquals] = &HandleLocalCompareEquals;
        table[(byte)Opcode.LocalJumpIfFalse] = &HandleLocalJumpIfFalse;
        table[(byte)Opcode.LocalJumpIfTrue] = &HandleLocalJumpIfTrue;
        table[(byte)Opcode.ReturnNull] = &HandleReturnNull;
        table[(byte)Opcode.ReturnTrue] = &HandleReturnTrue;
        table[(byte)Opcode.ReturnFalse] = &HandleReturnFalse;
        table[(byte)Opcode.LocalCompareNotEquals] = &HandleLocalCompareNotEquals;
        table[(byte)Opcode.LocalIncrement] = &HandleLocalIncrement;
        table[(byte)Opcode.LocalDecrement] = &HandleLocalDecrement;
        table[(byte)Opcode.LocalPushLocalPushSub] = &HandleLocalPushLocalPushSub;
        table[(byte)Opcode.LocalAddLocalAssign] = &HandleLocalAddLocalAssign;
        table[(byte)Opcode.LocalSubLocalAssign] = &HandleLocalSubLocalAssign;
        table[(byte)Opcode.LocalJumpIfNull] = &HandleLocalJumpIfNull;
        table[(byte)Opcode.LocalJumpIfNotNull] = &HandleLocalJumpIfNotNull;
        table[(byte)Opcode.LocalCompareEqualsJumpIfFalse] = &HandleLocalCompareEqualsJumpIfFalse;
        table[(byte)Opcode.LocalCompareNotEqualsJumpIfFalse] = &HandleLocalCompareNotEqualsJumpIfFalse;
        table[(byte)Opcode.LocalCompareLessThanJumpIfFalse] = &HandleLocalCompareLessThanJumpIfFalse;
        table[(byte)Opcode.LocalCompareGreaterThanJumpIfFalse] = &HandleLocalCompareGreaterThanJumpIfFalse;
        table[(byte)Opcode.LocalCompareLessThanOrEqualJumpIfFalse] = &HandleLocalCompareLessThanOrEqualJumpIfFalse;
        table[(byte)Opcode.LocalCompareGreaterThanOrEqualJumpIfFalse] = &HandleLocalCompareGreaterThanOrEqualJumpIfFalse;
        table[(byte)Opcode.LocalPushDereferenceField] = &HandleLocalPushDereferenceField;
        table[(byte)Opcode.LocalPushDereferenceCall] = &HandleLocalPushDereferenceCall;
        table[(byte)Opcode.LocalPushDereferenceIndex] = &HandleLocalPushDereferenceIndex;
        table[(byte)Opcode.LocalMulLocalAssign] = &HandleLocalMulLocalAssign;
        table[(byte)Opcode.LocalDivLocalAssign] = &HandleLocalDivLocalAssign;
        table[(byte)Opcode.LocalMulFloatAssign] = &HandleLocalMulFloatAssign;
        table[(byte)Opcode.LocalDivFloatAssign] = &HandleLocalDivFloatAssign;
        table[(byte)Opcode.PopN] = &HandlePopN;
        table[(byte)Opcode.LocalAddFloatAssign] = &HandleLocalAddFloatAssign;
        table[(byte)Opcode.LocalCompareLessThan] = &HandleLocalCompareLessThan;
        table[(byte)Opcode.LocalCompareGreaterThan] = &HandleLocalCompareGreaterThan;
        table[(byte)Opcode.LocalCompareLessThanOrEqual] = &HandleLocalCompareLessThanOrEqual;
        table[(byte)Opcode.LocalCompareGreaterThanOrEqual] = &HandleLocalCompareGreaterThanOrEqual;
        table[(byte)Opcode.LocalCompareLessThanFloatJumpIfFalse] = &HandleLocalCompareLessThanFloatJumpIfFalse;
        table[(byte)Opcode.LocalCompareGreaterThanFloatJumpIfFalse] = &HandleLocalCompareGreaterThanFloatJumpIfFalse;
        table[(byte)Opcode.LocalCompareLessThanOrEqualFloatJumpIfFalse] = &HandleLocalCompareLessThanOrEqualFloatJumpIfFalse;
        table[(byte)Opcode.LocalCompareGreaterThanOrEqualFloatJumpIfFalse] = &HandleLocalCompareGreaterThanOrEqualFloatJumpIfFalse;
        table[(byte)Opcode.LocalJumpIfFieldFalse] = &HandleLocalJumpIfFieldFalse;
        table[(byte)Opcode.LocalJumpIfFieldTrue] = &HandleLocalJumpIfFieldTrue;
        table[(byte)Opcode.LocalFieldTransfer] = &HandleLocalFieldTransfer;
        table[(byte)Opcode.GlobalJumpIfFalse] = &HandleGlobalJumpIfFalse;

        return table;
    }

    private static void HandleLocalFieldTransfer(ref InterpreterState state)
    {
        int pcForCache = state.PC - 1;
        int srcIdx = state.ReadInt32();
        int nameId = state.ReadInt32();
        int targetIdx = state.ReadInt32();
        PerformLocalFieldTransfer(ref state, srcIdx, nameId, targetIdx, pcForCache);
    }

    private static void HandleGlobalJumpIfFalse(ref InterpreterState state)
    {
        int pcForError = state.PC - 1;
        int globalIdx = state.ReadInt32();
        int address = state.ReadInt32();
        PerformGlobalJumpIfFalse(ref state, globalIdx, address, pcForError);
    }

    private static void HandleLocalCompareLessThanFloatJumpIfFalse(ref InterpreterState state) => HandleLocalCompareFloatJumpIfFalse(ref state, (a, b) => a < b);
    private static void HandleLocalCompareGreaterThanFloatJumpIfFalse(ref InterpreterState state) => HandleLocalCompareFloatJumpIfFalse(ref state, (a, b) => a > b);
    private static void HandleLocalCompareLessThanOrEqualFloatJumpIfFalse(ref InterpreterState state) => HandleLocalCompareFloatJumpIfFalse(ref state, (a, b) => a <= b);
    private static void HandleLocalCompareGreaterThanOrEqualFloatJumpIfFalse(ref InterpreterState state) => HandleLocalCompareFloatJumpIfFalse(ref state, (a, b) => a >= b);

    private static void HandleLocalJumpIfFieldFalse(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        int nameId = state.ReadInt32();
        int address = state.ReadInt32();
        var objValue = state.Locals[idx];
        if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
        {
            var name = state.Strings[nameId];
            var val = obj.GetVariable(name);
            if (val.IsFalse()) state.PC = address;
        }
        else throw new ScriptRuntimeException($"Field access on null object: {state.Strings[nameId]}", state.Proc, state.PC - 1, state.Thread);
    }

    private static void HandleLocalJumpIfFieldTrue(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        int nameId = state.ReadInt32();
        int address = state.ReadInt32();
        var objValue = state.Locals[idx];
        if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
        {
            var name = state.Strings[nameId];
            var val = obj.GetVariable(name);
            if (!val.IsFalse()) state.PC = address;
        }
        else throw new ScriptRuntimeException($"Field access on null object: {state.Strings[nameId]}", state.Proc, state.PC - 1, state.Thread);
    }

    private static void HandleUnknownOpcode(ref InterpreterState state)
    {
        throw new ScriptRuntimeException($"Unknown opcode: 0x{(byte)state.BytecodePtr[state.PC - 1]:X2}", state.Proc, state.PC - 1, state.Thread);
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
        state.Push(new DreamValue(state.ReadDouble()));
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
        PerformAdd(ref state);
    }

    private static void HandleSubtract(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Subtract", state.Proc, state.PC, state.Thread);
        PerformSubtract(ref state);
    }

    private static void HandleMultiply(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Multiply", state.Proc, state.PC, state.Thread);
        PerformMultiply(ref state);
    }

    private static void HandleDivide(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Divide", state.Proc, state.PC, state.Thread);
        PerformDivide(ref state);
    }

    private static void HandleCompareEquals(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareEquals", state.Proc, state.PC, state.Thread);
        PerformCompareEquals(ref state);
    }

    private static void HandleCompareNotEquals(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareNotEquals", state.Proc, state.PC, state.Thread);
        PerformCompareNotEquals(ref state);
    }

    private static void HandleCompareLessThan(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareLessThan", state.Proc, state.PC, state.Thread);
        PerformCompareLessThan(ref state);
    }

    private static void HandleCompareGreaterThan(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareGreaterThan", state.Proc, state.PC, state.Thread);
        PerformCompareGreaterThan(ref state);
    }

    private static void HandleCompareLessThanOrEqual(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareLessThanOrEqual", state.Proc, state.PC, state.Thread);
        PerformCompareLessThanOrEqual(ref state);
    }

    private static void HandleCompareGreaterThanOrEqual(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareGreaterThanOrEqual", state.Proc, state.PC, state.Thread);
        PerformCompareGreaterThanOrEqual(ref state);
    }

    private static void HandleCompareEquivalent(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareEquivalent", state.Proc, state.PC, state.Thread);
        var b = state.Stack[--state.StackPtr];
        var a = state.Stack[state.StackPtr - 1];
        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
        {
            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
                state.Stack[state.StackPtr - 1] = (a.UnsafeRawLong == b.UnsafeRawLong) ? DreamValue.True : DreamValue.False;
            else
                state.Stack[state.StackPtr - 1] = (a.UnsafeRawDouble == b.UnsafeRawDouble || Math.Abs(a.UnsafeRawDouble - b.UnsafeRawDouble) < 1e-5) ? DreamValue.True : DreamValue.False;
        }
        else
            state.Stack[state.StackPtr - 1] = a.Equals(b) ? DreamValue.True : DreamValue.False;
    }

    private static void HandleCompareNotEquivalent(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during CompareNotEquivalent", state.Proc, state.PC, state.Thread);
        var b = state.Stack[--state.StackPtr];
        var a = state.Stack[state.StackPtr - 1];
        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
        {
            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
                state.Stack[state.StackPtr - 1] = (a.UnsafeRawLong != b.UnsafeRawLong) ? DreamValue.True : DreamValue.False;
            else
                state.Stack[state.StackPtr - 1] = (a.UnsafeRawDouble != b.UnsafeRawDouble && Math.Abs(a.UnsafeRawDouble - b.UnsafeRawDouble) >= 1e-5) ? DreamValue.True : DreamValue.False;
        }
        else
            state.Stack[state.StackPtr - 1] = !a.Equals(b) ? DreamValue.True : DreamValue.False;
    }

    private static void HandleNegate(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during Negate", state.Proc, state.PC, state.Thread);
        var a = state.Stack[state.StackPtr - 1];
        if (a.Type <= DreamValueType.Integer)
        {
            if (a.Type == DreamValueType.Integer)
                state.Stack[state.StackPtr - 1] = new DreamValue(-a.UnsafeRawLong);
            else
                state.Stack[state.StackPtr - 1] = new DreamValue(-a.UnsafeRawDouble);
        }
        else
            state.Stack[state.StackPtr - 1] = -a;
    }

    private static void HandleBooleanNot(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during BooleanNot", state.Proc, state.PC, state.Thread);
        PerformBooleanNot(ref state);
    }

    private static void HandleCall(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        IDreamProc? targetProc = null;
        DreamObject? instance = null;

        switch (refType)
        {
            case DMReference.Type.GlobalProc:
                {
                    int procId = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    if (procId >= 0 && procId < state.Thread.Context.AllProcs.Count)
                        targetProc = state.Thread.Context.AllProcs[procId];
                }
                break;
            case DMReference.Type.SrcProc:
                {
                    int nameId = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    instance = state.Frame.Instance;
                    if (instance != null)
                    {
                        var name = state.Thread.Context.Strings[nameId];
                        targetProc = instance.ObjectType?.GetProc(name);
                        if (targetProc == null) state.Thread.Context.Procs.TryGetValue(name, out targetProc);
                    }
                }
                break;
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Stack[state.LocalBase + idx];
                    val.TryGetValue(out targetProc);
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Stack[state.ArgumentBase + idx];
                    val.TryGetValue(out targetProc);
                }
                break;
            case DMReference.Type.Global:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Thread.Context.GetGlobal(idx);
                    val.TryGetValue(out targetProc);
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var val = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                    val.TryGetValue(out targetProc);
                }
                break;
        }

        var argType = (DMCallArgumentsType)state.BytecodePtr[state.PC++];
        var argStackDelta = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;
        var unusedStackDelta = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;

        if (targetProc == null)
        {
            state.StackPtr -= argStackDelta;
            state.Push(DreamValue.Null);
            return;
        }

        if (targetProc is NativeProc nativeProc)
        {
            var argCount = argStackDelta;
            var stackBase = state.StackPtr - argStackDelta;
            var arguments = state.Stack.Slice(state.StackPtr - argCount, argCount);

            state.StackPtr = stackBase;
            try
            {
                var result = nativeProc.Call(state.Thread, instance, arguments);
                state.Push(result);
            }
            catch (Exception e)
            {
                throw new ScriptRuntimeException(e.Message, state.Proc, state.PC, state.Thread, e);
            }
            return;
        }

        state.Thread.SavePC(state.PC);
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.PerformCall(targetProc, instance, argStackDelta, argStackDelta);
    }

    private static void HandleCallStatement(ref InterpreterState state)
    {
        var argType = (DMCallArgumentsType)state.BytecodePtr[state.PC++];
        int argStackDelta = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;

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
        int address = state.ReadInt32();
        PerformJumpIfFalse(ref state, address);
    }

    private static void HandleJumpIfTrueReference(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    int address = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Locals[idx];
                    bool isFalse;
                    var type = val.Type;
                    if (type == DreamValueType.Null) isFalse = true;
                    else if (type == DreamValueType.Float) isFalse = val.UnsafeRawDouble == 0.0;
                    else if (type == DreamValueType.Integer) isFalse = val.UnsafeRawLong == 0;
                    else if (type == DreamValueType.String) isFalse = ((string)val.UnsafeRawObject!).Length == 0;
                    else isFalse = false;
                    if (!isFalse) state.PC = address;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    int address = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Arguments[idx];
                    bool isFalse;
                    var type = val.Type;
                    if (type == DreamValueType.Null) isFalse = true;
                    else if (type == DreamValueType.Float) isFalse = val.UnsafeRawDouble == 0.0;
                    else if (type == DreamValueType.Integer) isFalse = val.UnsafeRawLong == 0;
                    else if (type == DreamValueType.String) isFalse = ((string)val.UnsafeRawObject!).Length == 0;
                    else isFalse = false;
                    if (!isFalse) state.PC = address;
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    int address = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.Thread._stackPtr = state.StackPtr;
                    var val = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                    if (!val.IsFalse()) state.PC = address;
                }
                break;
        }
    }

    private static void HandleJumpIfFalseReference(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    int address = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Locals[idx];
                    bool isFalse;
                    var type = val.Type;
                    if (type == DreamValueType.Null) isFalse = true;
                    else if (type == DreamValueType.Float) isFalse = val.UnsafeRawDouble == 0.0;
                    else if (type == DreamValueType.Integer) isFalse = val.UnsafeRawLong == 0;
                    else if (type == DreamValueType.String) isFalse = ((string)val.UnsafeRawObject!).Length == 0;
                    else isFalse = false;
                    if (isFalse) state.PC = address;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    int address = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Arguments[idx];
                    bool isFalse;
                    var type = val.Type;
                    if (type == DreamValueType.Null) isFalse = true;
                    else if (type == DreamValueType.Float) isFalse = val.UnsafeRawDouble == 0.0;
                    else if (type == DreamValueType.Integer) isFalse = val.UnsafeRawLong == 0;
                    else if (type == DreamValueType.String) isFalse = ((string)val.UnsafeRawObject!).Length == 0;
                    else isFalse = false;
                    if (isFalse) state.PC = address;
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    int address = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.Thread._stackPtr = state.StackPtr;
                    var val = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                    if (val.IsFalse()) state.PC = address;
                }
                break;
        }
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
        state.Thread.Opcode_OutputReference(state.Proc, ref state.Frame, ref state.PC);
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleReturn(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_Return(ref state.Proc, ref state.PC);
        state.RefreshSpans();
        state.StackPtr = state.Thread._stackPtr;
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
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        var value = state.Pop();
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.GetLocal(idx) ^= value;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.GetArgument(idx) ^= value;
                }
                break;
            case DMReference.Type.Global:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Thread.Context.GetGlobal(idx);
                    state.Thread.Context.SetGlobal(idx, val ^ value);
                }
                break;
            case DMReference.Type.SrcField:
                {
                    var nameId = state.ReadInt32();
                    if (state.Frame.Instance is GameObject gameObject)
                    {
                        var name = state.Thread.Context.Strings[nameId];
                        int idx = gameObject.ObjectType?.GetVariableIndex(name) ?? -1;
                        var val = idx != -1 ? gameObject.GetVariableDirect(idx) : gameObject.GetVariable(name);
                        var newVal = val ^ value;
                        if (idx != -1) gameObject.SetVariableDirect(idx, newVal);
                        else gameObject.SetVariable(name, newVal);
                    }
                    else if (state.Frame.Instance != null)
                    {
                        var name = state.Thread.Context.Strings[nameId];
                        var val = state.Frame.Instance.GetVariable(name);
                        state.Frame.Instance.SetVariable(name, val ^ value);
                    }
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var refValue = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.SetReferenceValue(reference, ref state.Frame, refValue ^ value, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                }
                break;
        }
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
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        var value = state.Pop();
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.GetLocal(idx) <<= value;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.GetArgument(idx) <<= value;
                }
                break;
            case DMReference.Type.Global:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Thread.Context.GetGlobal(idx);
                    state.Thread.Context.SetGlobal(idx, val << value);
                }
                break;
            case DMReference.Type.SrcField:
                {
                    var nameId = state.ReadInt32();
                    if (state.Frame.Instance is GameObject gameObject)
                    {
                        var name = state.Thread.Context.Strings[nameId];
                        int idx = gameObject.ObjectType?.GetVariableIndex(name) ?? -1;
                        var val = idx != -1 ? gameObject.GetVariableDirect(idx) : gameObject.GetVariable(name);
                        var newVal = val << value;
                        if (idx != -1) gameObject.SetVariableDirect(idx, newVal);
                        else gameObject.SetVariable(name, newVal);
                    }
                    else if (state.Frame.Instance != null)
                    {
                        var name = state.Thread.Context.Strings[nameId];
                        var val = state.Frame.Instance.GetVariable(name);
                        state.Frame.Instance.SetVariable(name, val << value);
                    }
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var refValue = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.SetReferenceValue(reference, ref state.Frame, refValue << value, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                }
                break;
        }
    }

    private static void HandleBitShiftRight(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during BitShiftRight", state.Proc, state.PC, state.Thread);
        var b = state.Stack[--state.StackPtr];
        state.Stack[state.StackPtr - 1] >>= b;
    }

    private static void HandleBitShiftRightReference(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        var value = state.Pop();
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.GetLocal(idx) >>= value;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.GetArgument(idx) >>= value;
                }
                break;
            case DMReference.Type.Global:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Thread.Context.GetGlobal(idx);
                    state.Thread.Context.SetGlobal(idx, val >> value);
                }
                break;
            case DMReference.Type.SrcField:
                {
                    var nameId = state.ReadInt32();
                    if (state.Frame.Instance is GameObject gameObject)
                    {
                        var name = state.Thread.Context.Strings[nameId];
                        int idx = gameObject.ObjectType?.GetVariableIndex(name) ?? -1;
                        var val = idx != -1 ? gameObject.GetVariableDirect(idx) : gameObject.GetVariable(name);
                        var newVal = val >> value;
                        if (idx != -1) gameObject.SetVariableDirect(idx, newVal);
                        else gameObject.SetVariable(name, newVal);
                    }
                    else if (state.Frame.Instance != null)
                    {
                        var name = state.Thread.Context.Strings[nameId];
                        var val = state.Frame.Instance.GetVariable(name);
                        state.Frame.Instance.SetVariable(name, val >> value);
                    }
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var refValue = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.SetReferenceValue(reference, ref state.Frame, refValue >> value, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                }
                break;
        }
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

    private static void HandleLocalPushDereferenceCall(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        int nameId = state.ReadInt32();
        int pcForCache = state.PC - 5;
        var argType = (DMCallArgumentsType)state.ReadByte();
        int argStackDelta = state.ReadInt32();

        var objValue = state.GetLocal(idx);
        if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
        {
            IDreamProc? targetProc;
            ref var cache = ref state.Proc._inlineCache[pcForCache];
            if (cache.ObjectType == obj.ObjectType && cache.CachedProc != null)
            {
                targetProc = cache.CachedProc;
            }
            else
            {
                var procName = state.Strings[nameId];
                targetProc = obj.ObjectType?.GetProc(procName);
                if (targetProc == null)
                {
                    var varValue = obj.GetVariable(procName);
                    if (varValue.TryGetValue(out IDreamProc? procFromVar)) targetProc = procFromVar;
                }
                if (targetProc != null)
                {
                    cache.ObjectType = obj.ObjectType;
                    cache.CachedProc = targetProc;
                }
            }

            if (targetProc != null)
            {
                state.Thread.SavePC(state.PC);
                int argCount = argStackDelta;
                state.Thread._stackPtr = state.StackPtr;
                state.Thread.PerformCall(targetProc, obj, argCount, argCount);
                return;
            }
        }
        state.StackPtr -= argStackDelta;
        state.Push(DreamValue.Null);
    }

    private static void HandleLocalPushDereferenceIndex(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        var index = state.Pop();
        var objValue = state.GetLocal(idx);
        DreamValue val = DreamValue.Null;
        if (objValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
        {
            if (index.Type <= DreamValueType.Integer)
            {
                int i = (int)index.UnsafeRawDouble - 1;
                if (i >= 0 && i < list.Values.Count) val = list.Values[i];
            }
            else val = list.GetValue(index);
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
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.Push(state.Locals[idx]);
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.Push(state.Arguments[idx]);
                }
                break;
            case DMReference.Type.Global:
                {
                    int globalIdx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    if ((uint)globalIdx >= (uint)state.Globals.Count) throw new ScriptRuntimeException($"Invalid global index: {globalIdx}", state.Proc, state.PC - 5, state.Thread);
                    state.Push(state.Globals[globalIdx]);
                }
                break;
            case DMReference.Type.Src:
                state.Push(state.Frame.Instance != null ? new DreamValue(state.Frame.Instance) : DreamValue.Null);
                break;
            case DMReference.Type.World:
                state.Push(state.Thread.Context.World != null ? new DreamValue(state.Thread.Context.World) : DreamValue.Null);
                break;
            case DMReference.Type.SrcField:
                {
                    int nameId = *(int*)(state.BytecodePtr + state.PC);
                    int pcForCache = state.PC - 1;
                    state.PC += 4;
                    var instance = state.Frame.Instance;
                    if (instance != null)
                    {
                        ref var cache = ref state.Proc._inlineCache[pcForCache];
                        if (cache.ObjectType == instance.ObjectType)
                        {
                            state.Push(instance.GetVariableDirect(cache.VariableIndex));
                        }
                        else
                        {
                            var name = state.Strings[nameId];
                            int varIdx = instance.ObjectType?.GetVariableIndex(name) ?? -1;
                            if (varIdx != -1)
                            {
                                cache.ObjectType = instance.ObjectType;
                                cache.VariableIndex = varIdx;
                                state.Push(instance.GetVariableDirect(varIdx));
                            }
                            else state.Push(instance.GetVariable(name));
                        }
                    }
                    else state.Push(DreamValue.Null);
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var val = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                    state.Push(val);
                }
                break;
        }
    }

    private static void HandleAssign(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        var value = state.Stack[state.StackPtr - 1]; // peek
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.GetLocal(idx) = value;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.GetArgument(idx) = value;
                }
                break;
            case DMReference.Type.Global:
                {
                    int globalIdx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.Thread.Context.SetGlobal(globalIdx, value);
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    var val = state.Pop();
                    state.Thread._stackPtr = state.StackPtr;
                    state.Thread.SetReferenceValue(reference, ref state.Frame, val, 0);
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
        int jumpAddress = state.ReadInt32();
        PerformBooleanAnd(ref state, jumpAddress);
    }

    private static void HandleBooleanOr(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during BooleanOr", state.Proc, state.PC, state.Thread);
        int jumpAddress = state.ReadInt32();
        PerformBooleanOr(ref state, jumpAddress);
    }

    private static void HandleIncrement(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var val = ref state.GetLocal(idx);
                    var newVal = val + 1;
                    val = newVal;
                    state.Push(newVal);
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var val = ref state.GetArgument(idx);
                    var newVal = val + 1;
                    val = newVal;
                    state.Push(newVal);
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var value = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    var newValue = value + 1;
                    state.Thread.SetReferenceValue(reference, ref state.Frame, newValue, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                    state.Push(newValue);
                }
                break;
        }
    }

    private static void HandleDecrement(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var val = ref state.GetLocal(idx);
                    var newVal = val - 1;
                    val = newVal;
                    state.Push(newVal);
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var val = ref state.GetArgument(idx);
                    var newVal = val - 1;
                    val = newVal;
                    state.Push(newVal);
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var value = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    var newValue = value - 1;
                    state.Thread.SetReferenceValue(reference, ref state.Frame, newValue, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                    state.Push(newValue);
                }
                break;
        }
    }

    private static void HandleModulus(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Modulus", state.Proc, state.PC, state.Thread);
        var b = state.Stack[--state.StackPtr];
        var a = state.Stack[state.StackPtr - 1];
        state.Stack[state.StackPtr - 1] = a % b;
    }

    private static void HandleAssignInto(ref InterpreterState state)
    {
        var reference = state.ReadReference();
        var value = state.Stack[--state.StackPtr];
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.SetReferenceValue(reference, ref state.Frame, value, 0);
        state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
        state.StackPtr = state.Thread._stackPtr;
        state.Push(value);
    }

    private static void HandleModulusReference(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        var value = state.Pop();
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var val = ref state.GetLocal(idx);
                    if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                    {
                        double db = value.UnsafeRawDouble;
                        val = (db != 0) ? new DreamValue(val.UnsafeRawDouble % db) : DreamValue.False;
                    }
                    else val = val % value;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var val = ref state.GetArgument(idx);
                    if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                    {
                        double db = value.UnsafeRawDouble;
                        val = (db != 0) ? new DreamValue(val.UnsafeRawDouble % db) : DreamValue.False;
                    }
                    else val = val % value;
                }
                break;
            case DMReference.Type.Global:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Thread.Context.GetGlobal(idx);
                    if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                    {
                        double db = value.UnsafeRawDouble;
                        state.Thread.Context.SetGlobal(idx, (db != 0) ? new DreamValue(val.UnsafeRawDouble % db) : DreamValue.False);
                    }
                    else state.Thread.Context.SetGlobal(idx, val % value);
                }
                break;
            case DMReference.Type.SrcField:
                {
                    var nameId = state.ReadInt32();
                    if (state.Frame.Instance is GameObject gameObject)
                    {
                        var name = state.Thread.Context.Strings[nameId];
                        int idx = gameObject.ObjectType?.GetVariableIndex(name) ?? -1;
                        var val = idx != -1 ? gameObject.GetVariableDirect(idx) : gameObject.GetVariable(name);
                        DreamValue newVal;
                        if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                        {
                            double db = value.UnsafeRawDouble;
                            newVal = (db != 0) ? new DreamValue(val.UnsafeRawDouble % db) : DreamValue.False;
                        }
                        else newVal = val % value;
                        if (idx != -1) gameObject.SetVariableDirect(idx, newVal);
                        else gameObject.SetVariable(name, newVal);
                    }
                    else if (state.Frame.Instance != null)
                    {
                        var name = state.Thread.Context.Strings[nameId];
                        var val = state.Frame.Instance.GetVariable(name);
                        state.Frame.Instance.SetVariable(name, val % value);
                    }
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var refValue = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.SetReferenceValue(reference, ref state.Frame, refValue % value, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                }
                break;
        }
    }

    private static void HandleModulusModulus(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during ModulusModulus", state.Proc, state.PC, state.Thread);
        var b = state.Stack[--state.StackPtr];
        var a = state.Stack[state.StackPtr - 1];
        state.Stack[state.StackPtr - 1] = new DreamValue(SharedOperations.Modulo(a.GetValueAsDouble(), b.GetValueAsDouble()));
    }

    private static void HandleModulusModulusReference(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        var value = state.Pop();
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var val = ref state.GetLocal(idx);
                    double da = val.UnsafeRawDouble;
                    double db = value.UnsafeRawDouble;
                    val = (db != 0) ? new DreamValue(da - db * Math.Floor(da / db)) : DreamValue.False;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    ref var val = ref state.GetArgument(idx);
                    double da = val.UnsafeRawDouble;
                    double db = value.UnsafeRawDouble;
                    val = (db != 0) ? new DreamValue(da - db * Math.Floor(da / db)) : DreamValue.False;
                }
                break;
            case DMReference.Type.Global:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Thread.Context.GetGlobal(idx);
                    double da = val.UnsafeRawDouble;
                    double db = value.UnsafeRawDouble;
                    state.Thread.Context.SetGlobal(idx, (db != 0) ? new DreamValue(da - db * Math.Floor(da / db)) : DreamValue.False);
                }
                break;
            case DMReference.Type.SrcField:
                {
                    var nameId = state.ReadInt32();
                    if (state.Frame.Instance is GameObject gameObject)
                    {
                        var name = state.Thread.Context.Strings[nameId];
                        int idx = gameObject.ObjectType?.GetVariableIndex(name) ?? -1;
                        var val = idx != -1 ? gameObject.GetVariableDirect(idx) : gameObject.GetVariable(name);
                        double da = val.UnsafeRawDouble;
                        double db = value.UnsafeRawDouble;
                        var newVal = (db != 0) ? new DreamValue(da - db * Math.Floor(da / db)) : DreamValue.False;
                        if (idx != -1) gameObject.SetVariableDirect(idx, newVal);
                        else gameObject.SetVariable(name, newVal);
                    }
                    else if (state.Frame.Instance != null)
                    {
                        var name = state.Thread.Context.Strings[nameId];
                        var val = state.Frame.Instance.GetVariable(name);
                        state.Frame.Instance.SetVariable(name, new DreamValue(SharedOperations.Modulo(val.GetValueAsDouble(), value.GetValueAsDouble())));
                    }
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var refValue = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.SetReferenceValue(reference, ref state.Frame, new DreamValue(SharedOperations.Modulo(refValue.GetValueAsFloat(), value.GetValueAsFloat())), 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                }
                break;
        }
    }

    private static void HandleCreateList(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_CreateList(state.Proc, ref state.PC);
        state.Stack = state.Thread._stack.Array;
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleCreateAssociativeList(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_CreateAssociativeList(state.Proc, ref state.PC);
        state.Stack = state.Thread._stack.Array;
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleCreateStrictAssociativeList(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_CreateStrictAssociativeList(state.Proc, ref state.PC);
        state.Stack = state.Thread._stack.Array;
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleIsInList(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during IsInList", state.Proc, state.PC, state.Thread);
        var listValue = state.Stack[--state.StackPtr];
        var value = state.Stack[state.StackPtr - 1];

        bool result = false;
        if (listValue.Type == DreamValueType.DreamObject && listValue.TryGetValue(out DreamObject? obj) && obj is DreamList list)
        {
            result = list.Contains(value);
        }
        state.Stack[state.StackPtr - 1] = result ? DreamValue.True : DreamValue.False;
    }

    private static void HandleInput(ref InterpreterState state)
    {
        var ref1 = state.ReadReference();
        var ref2 = state.ReadReference();
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
            var name = state.Strings[nameId];
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
            if (index.Type <= DreamValueType.Integer)
            {
                int i = (int)index.UnsafeRawDouble - 1;
                if (i >= 0 && i < list.Values.Count) val = list.Values[i];
            }
            else val = list.GetValue(index);
        }
        else if (objValue.Type == DreamValueType.String && objValue.TryGetValue(out string? str) && str != null)
        {
            if (index.Type <= DreamValueType.Integer)
            {
                int i = (int)index.UnsafeRawDouble - 1;
                if (i >= 0 && i < str.Length) val = new DreamValue(str[i].ToString());
            }
        }
        state.Push(val);
    }

    private static void HandlePopReference(ref InterpreterState state)
    {
        var reference = state.ReadReference();
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleDereferenceCall(ref InterpreterState state)
    {
        int nameId = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;
        var argType = (DMCallArgumentsType)state.BytecodePtr[state.PC++];
        int argStackDelta = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;

        if (argStackDelta < 1 || state.StackPtr < argStackDelta)
            throw new ScriptRuntimeException($"Invalid argument stack delta for dereference call: {argStackDelta}", state.Proc, state.PC, state.Thread);

        var objValue = state.Stack[state.StackPtr - argStackDelta];
        if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
        {
            var procName = state.Strings[nameId];
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
                // Shift arguments to overwrite the object reference on the stack
                if (argCount > 0)
                {
                    state.Stack.Slice(stackBase + 1, argCount).CopyTo(state.Stack.Slice(stackBase));
                }
                state.StackPtr--;
                state.Thread._stackPtr = state.StackPtr;
                state.Thread.PerformCall(targetProc, obj, argCount, argCount);
                return;
            }
        }
        state.StackPtr -= argStackDelta;
        state.Push(DreamValue.Null);
    }

    private static void HandleInitial(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Initial", state.Proc, state.PC, state.Thread);
        var key = state.Stack[--state.StackPtr];
        var objValue = state.Stack[--state.StackPtr];
        DreamValue result = DreamValue.Null;

        ObjectType? type = null;
        if (objValue.Type == DreamValueType.DreamType)
        {
            objValue.TryGetValue(out type);
        }
        else if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
        {
            type = obj.ObjectType;
        }

        if (type != null && key.TryGetValue(out string? varName) && varName != null)
        {
            int index = type.GetVariableIndex(varName);
            if (index != -1 && index < type.FlattenedDefaultValues.Count)
                result = type.FlattenedDefaultValues[index];
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
        int enumeratorId = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        if (refType == DMReference.Type.Local)
        {
            int idx = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;
            int jumpAddress = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;
            var enumerator = state.Thread.GetEnumerator(enumeratorId);
            if (enumerator != null && enumerator.MoveNext())
                state.Stack[state.LocalBase + idx] = enumerator.Current;
            else
                state.PC = jumpAddress;
        }
        else if (refType == DMReference.Type.Argument)
        {
            int idx = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;
            int jumpAddress = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;
            var enumerator = state.Thread.GetEnumerator(enumeratorId);
            if (enumerator != null && enumerator.MoveNext())
                state.Stack[state.ArgumentBase + idx] = enumerator.Current;
            else
                state.PC = jumpAddress;
        }
        else
        {
            state.PC--;
            var reference = state.ReadReference();
            int jumpAddress = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;
            var enumerator = state.Thread.GetEnumerator(enumeratorId);
            if (enumerator != null && enumerator.MoveNext())
            {
                state.Thread.SetReferenceValue(reference, ref state.Frame, enumerator.Current, 0);
                state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            }
            else state.PC = jumpAddress;
        }
    }

    private static void HandleEnumerateAssoc(ref InterpreterState state)
    {
        int enumeratorId = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;
        var refType1 = (DMReference.Type)state.BytecodePtr[state.PC];
        if (refType1 == DMReference.Type.Local && (DMReference.Type)state.BytecodePtr[state.PC + 5] == DMReference.Type.Local)
        {
            state.PC++;
            int idx1 = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;
            state.PC++;
            int idx2 = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;
            int jumpAddress = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;

            if ((uint)idx1 >= (uint)state.Locals.Length) throw new ScriptRuntimeException("Local index 1 out of bounds", state.Proc, state.PC - 14, state.Thread);
            if ((uint)idx2 >= (uint)state.Locals.Length) throw new ScriptRuntimeException("Local index 2 out of bounds", state.Proc, state.PC - 14, state.Thread);

            var enumerator = state.Thread.GetEnumerator(enumeratorId);
            if (enumerator != null && enumerator.MoveNext())
            {
                var key = enumerator.Current;
                state.Locals[idx2] = key;
                var list = state.Thread.GetEnumeratorList(enumeratorId);
                state.Locals[idx1] = list != null ? list.GetValue(key) : DreamValue.Null;
            }
            else state.PC = jumpAddress;
        }
        else if (refType1 == DMReference.Type.Argument && (DMReference.Type)state.BytecodePtr[state.PC + 5] == DMReference.Type.Argument)
        {
            state.PC++;
            int idx1 = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;
            state.PC++;
            int idx2 = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;
            int jumpAddress = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;

            if ((uint)idx1 >= (uint)state.Arguments.Length) throw new ScriptRuntimeException("Argument index 1 out of bounds", state.Proc, state.PC - 14, state.Thread);
            if ((uint)idx2 >= (uint)state.Arguments.Length) throw new ScriptRuntimeException("Argument index 2 out of bounds", state.Proc, state.PC - 14, state.Thread);

            var enumerator = state.Thread.GetEnumerator(enumeratorId);
            if (enumerator != null && enumerator.MoveNext())
            {
                var key = enumerator.Current;
                state.Arguments[idx2] = key;
                var list = state.Thread.GetEnumeratorList(enumeratorId);
                state.Arguments[idx1] = list != null ? list.GetValue(key) : DreamValue.Null;
            }
            else state.PC = jumpAddress;
        }
        else
        {
            var assocRef = state.ReadReference();
            var outputRef = state.ReadReference();
            int jumpAddress = *(int*)(state.BytecodePtr + state.PC);
            state.PC += 4;
            var enumerator = state.Thread.GetEnumerator(enumeratorId);
            if (enumerator != null && enumerator.MoveNext())
            {
                var key = enumerator.Current;
                state.Thread.SetReferenceValue(outputRef, ref state.Frame, key, 0);
                state.Thread.PopCount(state.Thread.GetReferenceStackSize(outputRef));
                DreamValue value = DreamValue.Null;
                var list = state.Thread.GetEnumeratorList(enumeratorId);
                if (list != null) value = list.GetValue(key);
                state.Thread.SetReferenceValue(assocRef, ref state.Frame, value, 0);
                state.Thread.PopCount(state.Thread.GetReferenceStackSize(assocRef));
            }
            else state.PC = jumpAddress;
        }
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
        state.Thread.Opcode_Append(state.Proc, ref state.Frame, ref state.PC);
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleRemove(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_Remove(state.Proc, ref state.Frame, ref state.PC);
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleDeleteObject(ref InterpreterState state)
    {
        var value = state.Stack[--state.StackPtr];
        if (value.TryGetValueAsGameObject(out var obj)) state.Thread.Context.GameState?.RemoveGameObject(obj);
    }

    private static void HandleProb(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during Prob", state.Proc, state.PC, state.Thread);
        var chanceValue = state.Stack[--state.StackPtr];
        if (chanceValue.TryGetValue(out double chance))
        {
            state.Push(new DreamValue(Random.Shared.NextDouble() * 100 < chance ? 1 : 0));
        }
        else
        {
            state.Push(new DreamValue(0));
        }
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
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during GetDist", state.Proc, state.PC, state.Thread);
        var b = state.Stack[--state.StackPtr];
        var a = state.Stack[--state.StackPtr];

        if (a.TryGetValueAsGameObject(out var objA) && b.TryGetValueAsGameObject(out var objB))
        {
            if (objA.Z != objB.Z)
            {
                state.Push(new DreamValue(1000000.0));
                return;
            }
            var dx = Math.Abs(objA.X - objB.X);
            var dy = Math.Abs(objA.Y - objB.Y);
            state.Push(new DreamValue((double)Math.Max(dx, dy)));
        }
        else
        {
            state.Push(new DreamValue(0.0));
        }
    }

    private static void HandleGetDir(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_GetDir();
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleMassConcatenation(ref InterpreterState state)
    {
        int count = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;
        if (count < 0 || count > state.StackPtr)
            throw new ScriptRuntimeException($"Invalid concatenation count: {count}", state.Proc, state.PC, state.Thread);

        if (count == 0)
        {
            state.Push(new DreamValue(""));
            return;
        }

        int baseIdx = state.StackPtr - count;
        var result = _formatStringBuilder.Value!;
        result.Clear();

        for (int i = 0; i < count; i++)
        {
            state.Stack[baseIdx + i].AppendTo(result);
            if (result.Length > 1073741824)
                throw new ScriptRuntimeException("Maximum string length exceeded during concatenation", state.Proc, state.PC, state.Thread);
        }

        state.StackPtr -= count;
        state.Push(new DreamValue(result.ToString()));
    }

    private static readonly ThreadLocal<System.Text.StringBuilder> _formatStringBuilder = new(() => new System.Text.StringBuilder(256));

    private static void HandleFormatString(ref InterpreterState state)
    {
        int stringId = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;
        int formatCount = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;

        if (stringId < 0 || stringId >= state.Thread.Context.Strings.Count)
            throw new ScriptRuntimeException($"Invalid string ID: {stringId}", state.Proc, state.PC, state.Thread);
        if (formatCount < 0 || formatCount > state.StackPtr)
            throw new ScriptRuntimeException($"Invalid format count: {formatCount}", state.Proc, state.PC, state.Thread);

        var formatString = state.Thread.Context.Strings[stringId];
        var values = state.Stack.Slice(state.StackPtr - formatCount, formatCount);

        var result = _formatStringBuilder.Value!;
        result.Clear();
        if (result.Capacity < formatString.Length + formatCount * 8) result.Capacity = formatString.Length + formatCount * 8;

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
                        values[valueIndex++].AppendTo(result);
                        if (result.Length > 1073741824)
                            throw new ScriptRuntimeException("Maximum string length exceeded during formatting", state.Proc, state.PC, state.Thread);
                    }
                }
                else
                {
                    result.Append(c);
                }
            }
            else
            {
                result.Append(c);
            }
        }

        state.StackPtr -= formatCount;
        state.Push(new DreamValue(result.ToString()));
    }

    private static void HandlePower(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during Power", state.Proc, state.PC, state.Thread);
        var b = state.Stack[--state.StackPtr];
        var a = state.Stack[state.StackPtr - 1];
        double da = a.GetValueAsDouble();
        double db = b.GetValueAsDouble();

        // Optimized fast-paths for common powers
        if (db == 2.0) state.Stack[state.StackPtr - 1] = new DreamValue(da * da);
        else if (db == 0.5) state.Stack[state.StackPtr - 1] = new DreamValue(Math.Sqrt(da));
        else if (db == 1.0) state.Stack[state.StackPtr - 1] = a;
        else if (db == 0.0) state.Stack[state.StackPtr - 1] = DreamValue.True;
        else state.Stack[state.StackPtr - 1] = new DreamValue(Math.Pow(da, db));
    }

    private static void HandleSqrt(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during Sqrt", state.Proc, state.PC, state.Thread);
        var a = state.Stack[state.StackPtr - 1];
        state.Stack[state.StackPtr - 1] = new DreamValue(Math.Sqrt(a.GetValueAsDouble()));
    }

    private static void HandleAbs(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during Abs", state.Proc, state.PC, state.Thread);
        var a = state.Stack[state.StackPtr - 1];
        state.Stack[state.StackPtr - 1] = new DreamValue(Math.Abs(a.GetValueAsDouble()));
    }

    private static void HandleMultiplyReference(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.ReadByte();
        var value = state.Pop();
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = state.ReadInt32();
                    ref var val = ref state.GetLocal(idx);
                    if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                    {
                        if (val.Type == DreamValueType.Integer && value.Type == DreamValueType.Integer)
                            val = new DreamValue(val.UnsafeRawLong * value.UnsafeRawLong);
                        else
                            val = new DreamValue(val.UnsafeRawDouble * value.UnsafeRawDouble);
                    }
                    else
                        val = val * value;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = state.ReadInt32();
                    ref var val = ref state.GetArgument(idx);
                    if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                    {
                        if (val.Type == DreamValueType.Integer && value.Type == DreamValueType.Integer)
                            val = new DreamValue(val.UnsafeRawLong * value.UnsafeRawLong);
                        else
                            val = new DreamValue(val.UnsafeRawDouble * value.UnsafeRawDouble);
                    }
                    else
                        val = val * value;
                }
                break;
            case DMReference.Type.Global:
                {
                    int idx = state.ReadInt32();
                    var val = state.Thread.Context.GetGlobal(idx);
                    if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                    {
                        if (val.Type == DreamValueType.Integer && value.Type == DreamValueType.Integer)
                            state.Thread.Context.SetGlobal(idx, new DreamValue(val.UnsafeRawLong * value.UnsafeRawLong));
                        else
                            state.Thread.Context.SetGlobal(idx, new DreamValue(val.UnsafeRawDouble * value.UnsafeRawDouble));
                    }
                    else
                        state.Thread.Context.SetGlobal(idx, val * value);
                }
                break;
            case DMReference.Type.SrcField:
                {
                    var nameId = state.ReadInt32();
                    if (state.Frame.Instance is GameObject gameObject)
                    {
                        var name = state.Thread.Context.Strings[nameId];
                        int idx = gameObject.ObjectType?.GetVariableIndex(name) ?? -1;
                        var val = idx != -1 ? gameObject.GetVariableDirect(idx) : gameObject.GetVariable(name);
                        DreamValue newVal;
                        if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                        {
                            if (val.Type == DreamValueType.Integer && value.Type == DreamValueType.Integer)
                                newVal = new DreamValue(val.UnsafeRawLong * value.UnsafeRawLong);
                            else
                                newVal = new DreamValue(val.UnsafeRawDouble * value.UnsafeRawDouble);
                        }
                        else
                            newVal = val * value;
                        if (idx != -1) gameObject.SetVariableDirect(idx, newVal);
                        else gameObject.SetVariable(name, newVal);
                    }
                    else if (state.Frame.Instance != null)
                    {
                        var name = state.Thread.Context.Strings[nameId];
                        var val = state.Frame.Instance.GetVariable(name);
                        state.Frame.Instance.SetVariable(name, val * value);
                    }
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var refValue = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.SetReferenceValue(reference, ref state.Frame, refValue * value, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                }
                break;
        }
    }

    private static void HandleSin(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during Sin", state.Proc, state.PC, state.Thread);
        var a = state.Stack[state.StackPtr - 1];
        state.Stack[state.StackPtr - 1] = new DreamValue(Math.Sin(a.GetValueAsDouble() * (Math.PI / 180.0)));
    }

    private static void HandleDivideReference(ref InterpreterState state)
    {
        var refType = (DMReference.Type)state.ReadByte();
        var value = state.Pop();
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = state.ReadInt32();
                    ref var val = ref state.GetLocal(idx);
                    if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                    {
                        double dv = value.UnsafeRawDouble;
                        val = (dv != 0) ? new DreamValue(val.UnsafeRawDouble / dv) : new DreamValue(0.0);
                    }
                    else
                        val = val / value;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = state.ReadInt32();
                    ref var val = ref state.GetArgument(idx);
                    if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                    {
                        double dv = value.UnsafeRawDouble;
                        val = (dv != 0) ? new DreamValue(val.UnsafeRawDouble / dv) : new DreamValue(0.0);
                    }
                    else
                        val = val / value;
                }
                break;
            case DMReference.Type.Global:
                {
                    int idx = state.ReadInt32();
                    var val = state.Thread.Context.GetGlobal(idx);
                    if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                    {
                        double dv = value.UnsafeRawDouble;
                        state.Thread.Context.SetGlobal(idx, (dv != 0) ? new DreamValue(val.UnsafeRawDouble / dv) : new DreamValue(0.0));
                    }
                    else
                        state.Thread.Context.SetGlobal(idx, val / value);
                }
                break;
            case DMReference.Type.SrcField:
                {
                    var nameId = state.ReadInt32();
                    if (state.Frame.Instance is GameObject gameObject)
                    {
                        var name = state.Thread.Context.Strings[nameId];
                        int idx = gameObject.ObjectType?.GetVariableIndex(name) ?? -1;
                        var val = idx != -1 ? gameObject.GetVariableDirect(idx) : gameObject.GetVariable(name);
                        DreamValue newVal;
                        if (val.Type <= DreamValueType.Integer && value.Type <= DreamValueType.Integer)
                        {
                            double dv = value.UnsafeRawDouble;
                            newVal = (dv != 0) ? new DreamValue(val.UnsafeRawDouble / dv) : new DreamValue(0.0);
                        }
                        else
                            newVal = val / value;
                        if (idx != -1) gameObject.SetVariableDirect(idx, newVal);
                        else gameObject.SetVariable(name, newVal);
                    }
                    else if (state.Frame.Instance != null)
                    {
                        var name = state.Thread.Context.Strings[nameId];
                        var val = state.Frame.Instance.GetVariable(name);
                        state.Frame.Instance.SetVariable(name, val / value);
                    }
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    state.Thread._stackPtr = state.StackPtr;
                    var refValue = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.SetReferenceValue(reference, ref state.Frame, refValue / value, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                }
                break;
        }
    }

    private static void HandleCos(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during Cos", state.Proc, state.PC, state.Thread);
        var a = state.Stack[state.StackPtr - 1];
        state.Stack[state.StackPtr - 1] = new DreamValue(Math.Cos(a.GetValueAsDouble() * (Math.PI / 180.0)));
    }

    private static void HandleTan(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during Tan", state.Proc, state.PC, state.Thread);
        var a = state.Stack[state.StackPtr - 1];
        state.Stack[state.StackPtr - 1] = new DreamValue(Math.Tan(a.GetValueAsDouble() * (Math.PI / 180.0)));
    }

    private static void HandleArcSin(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during ArcSin", state.Proc, state.PC, state.Thread);
        var a = state.Stack[state.StackPtr - 1];
        state.Stack[state.StackPtr - 1] = new DreamValue(Math.Asin(a.GetValueAsDouble()) * (180.0 / Math.PI));
    }

    private static void HandleArcCos(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during ArcCos", state.Proc, state.PC, state.Thread);
        var a = state.Stack[state.StackPtr - 1];
        state.Stack[state.StackPtr - 1] = new DreamValue(Math.Acos(a.GetValueAsDouble()) * (180.0 / Math.PI));
    }

    private static void HandleArcTan(ref InterpreterState state)
    {
        if (state.StackPtr < 1) throw new ScriptRuntimeException("Stack underflow during ArcTan", state.Proc, state.PC, state.Thread);
        var a = state.Stack[state.StackPtr - 1];
        state.Stack[state.StackPtr - 1] = new DreamValue(Math.Atan(a.GetValueAsDouble()) * (180.0 / Math.PI));
    }

    private static void HandleArcTan2(ref InterpreterState state)
    {
        if (state.StackPtr < 2) throw new ScriptRuntimeException("Stack underflow during ArcTan2", state.Proc, state.PC, state.Thread);
        var y = state.Stack[--state.StackPtr];
        var x = state.Stack[state.StackPtr - 1];
        state.Stack[state.StackPtr - 1] = new DreamValue(Math.Atan2(y.GetValueAsDouble(), x.GetValueAsDouble()) * (180.0 / Math.PI));
    }

    private static void HandleLog(ref InterpreterState state)
    {
        var baseValue = state.Stack[--state.StackPtr];
        var x = state.Stack[--state.StackPtr];
        state.Push(new DreamValue(Math.Log(x.GetValueAsDouble(), baseValue.GetValueAsDouble())));
    }

    private static void HandleLogE(ref InterpreterState state)
    {
        state.Push(new DreamValue(Math.Log(state.Stack[--state.StackPtr].GetValueAsDouble())));
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
        state.Stack = state.Thread._stack.Array;
        state.StackPtr = state.Thread._stackPtr;
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

        if (val.Type <= DreamValueType.Integer && min.Type <= DreamValueType.Integer && max.Type <= DreamValueType.Integer)
        {
            double dv = val.UnsafeRawDouble;
            state.Push(dv >= min.UnsafeRawDouble && dv <= max.UnsafeRawDouble ? DreamValue.True : DreamValue.False);
        }
        else
        {
            state.Push(val >= min && val <= max ? DreamValue.True : DreamValue.False);
        }
    }

    private static void HandleThrow(ref InterpreterState state)
    {
        var value = state.Stack[--state.StackPtr];
        var e = new ScriptRuntimeException(value.ToString(), state.Proc, state.PC, thread: state.Thread) { ThrownValue = value };
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.HandleException(e);
        state.Stack = state.Thread._stack.Array;
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleTry(ref InterpreterState state)
    {
        var catchAddress = state.ReadInt32();
        var catchRef = state.ReadReference();
        state.Thread.PushTryBlock(new TryBlock { CatchAddress = catchAddress, CallStackDepth = state.Thread.CallStackCount, StackPointer = state.StackPtr, CatchReference = catchRef });
    }

    private static void HandleTryNoValue(ref InterpreterState state)
    {
        var catchAddress = state.ReadInt32();
        state.Thread.PushTryBlock(new TryBlock { CatchAddress = catchAddress, CallStackDepth = state.Thread.CallStackCount, StackPointer = state.StackPtr, CatchReference = null });
    }

    private static void HandleEndTry(ref InterpreterState state)
    {
        state.Thread.PopTryBlock();
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
        if (delay.TryGetValue(out double seconds) && seconds > 0) newThread.Sleep((float)seconds / 10.0f);
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
        var reference = state.ReadReference();
        var value = state.Pop();
        state.Thread._stackPtr = state.StackPtr;
        var listValue = state.Thread.GetReferenceValue(reference, ref state.Frame);
        if (listValue.Type == DreamValueType.DreamObject && listValue.TryGetValue(out DreamObject? obj) && obj is DreamList list) list.AddValue(value);
        state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleAssignNoPush(ref InterpreterState state)
    {
        var reference = state.ReadReference();
        var value = state.Pop();
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.SetReferenceValue(reference, ref state.Frame, value, 0);
        state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandlePushRefAndDereferenceField(ref InterpreterState state)
    {
        var reference = state.ReadReference();
        var fieldNameId = state.ReadInt32();
        var fieldName = state.Thread.Context.Strings[fieldNameId];
        state.Thread._stackPtr = state.StackPtr;
        var objValue = state.Thread.GetReferenceValue(reference, ref state.Frame);
        DreamValue val = DreamValue.Null;
        if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj != null) val = obj.GetVariable(fieldName);
        state.StackPtr = state.Thread._stackPtr;
        state.Push(val);
    }

    private static void HandlePushNRefs(ref InterpreterState state)
    {
        int count = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;

        for (int i = 0; i < count; i++)
        {
            var reference = state.ReadReference();
            state.Thread._stackPtr = state.StackPtr;
            var val = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
            state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
            state.StackPtr = state.Thread._stackPtr;
            state.Push(val);
        }
    }

    private static void HandlePushNFloats(ref InterpreterState state)
    {
        var count = state.ReadInt32();
        for (int i = 0; i < count; i++) state.Push(new DreamValue(state.ReadDouble()));
    }

    private static void HandlePushStringFloat(ref InterpreterState state)
    {
        var stringId = state.ReadInt32();
        var value = state.ReadDouble();
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
        var value = state.ReadDouble();
        var jumpAddress = state.ReadInt32();
        var switchValue = state.Stack[state.StackPtr - 1];
        if (switchValue.Type <= DreamValueType.Integer && switchValue.UnsafeRawDouble == value) state.PC = jumpAddress;
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
        var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
        switch (refType)
        {
            case DMReference.Type.Local:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    int address = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Locals[idx];
                    bool isFalse;
                    var type = val.Type;
                    if (type == DreamValueType.Null) isFalse = true;
                    else if (type == DreamValueType.Float) isFalse = val.UnsafeRawDouble == 0.0;
                    else if (type == DreamValueType.Integer) isFalse = val.UnsafeRawLong == 0;
                    else if (type == DreamValueType.String) isFalse = ((string)val.UnsafeRawObject!).Length == 0;
                    else isFalse = false;
                    if (isFalse) state.PC = address;
                }
                break;
            case DMReference.Type.Argument:
                {
                    int idx = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    int address = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    var val = state.Arguments[idx];
                    bool isFalse;
                    var type = val.Type;
                    if (type == DreamValueType.Null) isFalse = true;
                    else if (type == DreamValueType.Float) isFalse = val.UnsafeRawDouble == 0.0;
                    else if (type == DreamValueType.Integer) isFalse = val.UnsafeRawLong == 0;
                    else if (type == DreamValueType.String) isFalse = ((string)val.UnsafeRawObject!).Length == 0;
                    else isFalse = false;
                    if (isFalse) state.PC = address;
                }
                break;
            default:
                {
                    state.PC--;
                    var reference = state.ReadReference();
                    int address = *(int*)(state.BytecodePtr + state.PC);
                    state.PC += 4;
                    state.Thread._stackPtr = state.StackPtr;
                    var val = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
                    state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                    state.StackPtr = state.Thread._stackPtr;
                    if (val.IsFalse()) state.PC = address;
                }
                break;
        }
    }

    private static void HandleReturnFloat(ref InterpreterState state)
    {
        state.Push(new DreamValue(state.ReadDouble()));
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_Return(ref state.Proc, ref state.PC);
        state.RefreshSpans();
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleNPushFloatAssign(ref InterpreterState state)
    {
        int n = state.ReadInt32();
        double value = state.ReadDouble();
        var dv = new DreamValue(value);

        for (int i = 0; i < n; i++)
        {
            var refType = (DMReference.Type)state.BytecodePtr[state.PC++];
            if (refType == DMReference.Type.Local)
            {
                int idx = *(int*)(state.BytecodePtr + state.PC);
                state.PC += 4;
                state.Stack[state.LocalBase + idx] = dv;
            }
            else if (refType == DMReference.Type.Argument)
            {
                int idx = *(int*)(state.BytecodePtr + state.PC);
                state.PC += 4;
                state.Stack[state.ArgumentBase + idx] = dv;
            }
            else if (refType == DMReference.Type.Global)
            {
                int globalIdx = *(int*)(state.BytecodePtr + state.PC);
                state.PC += 4;
                state.Thread.Context.SetGlobal(globalIdx, dv);
            }
            else
            {
                state.PC--;
                var reference = state.ReadReference();
                state.Thread._stackPtr = state.StackPtr;
                state.Thread.SetReferenceValue(reference, ref state.Frame, dv, 0);
                state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
                state.StackPtr = state.Thread._stackPtr;
            }
        }
        state.Push(dv);
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
        var reference = state.ReadReference();
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.SetReferenceValue(reference, ref state.Frame, DreamValue.Null, 0);
        state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleIndexRefWithString(ref InterpreterState state)
    {
        var reference = state.ReadReference();
        var stringId = state.ReadInt32();
        var stringValue = new DreamValue(state.Thread.Context.Strings[stringId]);
        state.Thread._stackPtr = state.StackPtr;
        var objValue = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
        state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
        DreamValue result = DreamValue.Null;
        if (objValue.Type == DreamValueType.DreamObject && objValue.TryGetValue(out DreamObject? obj) && obj is DreamList list) result = list.GetValue(stringValue);
        state.Push(result);
        state.Stack = state.Thread._stack.Array;
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleReturnReferenceValue(ref InterpreterState state)
    {
        var reference = state.ReadReference();
        state.Thread._stackPtr = state.StackPtr;
        var val = state.Thread.GetReferenceValue(reference, ref state.Frame, 0);
        state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
        state.Thread.Push(val);
        state.Thread.Opcode_Return(ref state.Proc, ref state.PC);
        state.RefreshSpans();
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandlePushFloatAssign(ref InterpreterState state)
    {
        var value = state.ReadDouble();
        var reference = state.ReadReference();
        var dv = new DreamValue(value);
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.SetReferenceValue(reference, ref state.Frame, dv, 0);
        state.Thread.PopCount(state.Thread.GetReferenceStackSize(reference));
        state.Thread.Push(dv);
        state.Stack = state.Thread._stack.Array;
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandlePushLocal(ref InterpreterState state)
    {
        int idx = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;
        if ((uint)idx >= (uint)state.Proc.LocalVariableCount) throw new ScriptRuntimeException("Local index out of bounds", state.Proc, state.PC, state.Thread);
        state.Push(state.Stack[state.LocalBase + idx]);
    }

    private static void HandleAssignLocal(ref InterpreterState state)
    {
        int idx = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;
        if ((uint)idx >= (uint)state.Proc.LocalVariableCount) throw new ScriptRuntimeException("Local index out of bounds", state.Proc, state.PC, state.Thread);
        state.Stack[state.LocalBase + idx] = state.Stack[state.StackPtr - 1];
    }

    private static void HandlePushArgument(ref InterpreterState state)
    {
        int idx = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;
        if ((uint)idx >= (uint)state.Proc.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", state.Proc, state.PC, state.Thread);
        state.Push(state.Stack[state.ArgumentBase + idx]);
    }

    private static void HandleLocalPushLocalPushAdd(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();

        var a = state.GetLocal(idx1);
        var b = state.GetLocal(idx2);

        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
        {
            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
                state.Push(new DreamValue(a.UnsafeRawLong + b.UnsafeRawLong));
            else
                state.Push(new DreamValue(a.UnsafeRawDouble + b.UnsafeRawDouble));
        }
        else
            state.Push(a + b);
    }

    private static void HandleLocalAddFloat(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        double val = state.ReadDouble();

        var a = state.GetLocal(idx);
        if (a.Type <= DreamValueType.Integer)
            state.Push(new DreamValue(a.UnsafeRawDouble + val));
        else
            state.Push(a + val);
    }

    private static void HandleLocalMulAdd(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        int idx3 = state.ReadInt32();

        var a = state.GetLocal(idx1);
        var b = state.GetLocal(idx2);
        var c = state.GetLocal(idx3);

        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer && c.Type <= DreamValueType.Integer)
        {
            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer && c.Type == DreamValueType.Integer)
                state.Push(new DreamValue(a.UnsafeRawLong * b.UnsafeRawLong + c.UnsafeRawLong));
            else
                state.Push(new DreamValue(a.UnsafeRawDouble * b.UnsafeRawDouble + c.UnsafeRawDouble));
        }
        else
            state.Push(a * b + c);
    }

    private static void HandleGetBuiltinVar(ref InterpreterState state)
    {
        var varType = (BuiltinVar)state.ReadByte();
        var instance = state.Frame.Instance as GameObject;
        if (instance != null)
        {
            switch (varType)
            {
                case BuiltinVar.Icon: state.Push(new DreamValue(instance.Icon)); break;
                case BuiltinVar.IconState: state.Push(new DreamValue(instance.IconState)); break;
                case BuiltinVar.Dir: state.Push(new DreamValue((double)instance.Dir)); break;
                case BuiltinVar.Alpha: state.Push(new DreamValue(instance.Alpha)); break;
                case BuiltinVar.Color: state.Push(new DreamValue(instance.Color)); break;
                case BuiltinVar.Layer: state.Push(new DreamValue(instance.Layer)); break;
                case BuiltinVar.PixelX: state.Push(new DreamValue(instance.PixelX)); break;
                case BuiltinVar.PixelY: state.Push(new DreamValue(instance.PixelY)); break;
                default: state.Push(DreamValue.Null); break;
            }
        }
        else
        {
            state.Push(DreamValue.Null);
        }
    }

    private static void HandleSetBuiltinVar(ref InterpreterState state)
    {
        var varType = (BuiltinVar)state.ReadByte();
        var val = state.Stack[--state.StackPtr];
        var instance = state.Frame.Instance as GameObject;
        if (instance != null)
        {
            switch (varType)
            {
                case BuiltinVar.Icon: val.TryGetValue(out string? s); if (s != null) instance.Icon = s; break;
                case BuiltinVar.IconState: val.TryGetValue(out string? s2); if (s2 != null) instance.IconState = s2; break;
                case BuiltinVar.Dir: instance.Dir = (int)val.GetValueAsFloat(); break;
                case BuiltinVar.Alpha: instance.Alpha = val.GetValueAsFloat(); break;
                case BuiltinVar.Color: val.TryGetValue(out string? s3); if (s3 != null) instance.Color = s3; break;
                case BuiltinVar.Layer: instance.Layer = val.GetValueAsFloat(); break;
                case BuiltinVar.PixelX: instance.PixelX = val.GetValueAsFloat(); break;
                case BuiltinVar.PixelY: instance.PixelY = val.GetValueAsFloat(); break;
            }
        }
    }

    private static void HandleLocalPushReturn(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        state.Push(state.GetLocal(idx));
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_Return(ref state.Proc, ref state.PC);
        state.Stack = state.Thread._stack.Array;
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleLocalCompareEquals(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();

        var a = state.GetLocal(idx1);
        var b = state.GetLocal(idx2);
        state.Push(a == b ? DreamValue.True : DreamValue.False);
    }

    private static void HandleLocalJumpIfFalse(ref InterpreterState state)
    {
        int pcForError = state.PC - 1;
        int idx = state.ReadInt32();
        int address = state.ReadInt32();
        PerformLocalJumpIfFalse(ref state, idx, address, pcForError);
    }

    private static void HandleLocalJumpIfTrue(ref InterpreterState state)
    {
        int pcForError = state.PC - 1;
        int idx = state.ReadInt32();
        int address = state.ReadInt32();
        PerformLocalJumpIfTrue(ref state, idx, address, pcForError);
    }

    private static void HandleReturnNull(ref InterpreterState state)
    {
        state.Push(DreamValue.Null);
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_Return(ref state.Proc, ref state.PC);
        state.RefreshSpans();
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleReturnTrue(ref InterpreterState state)
    {
        state.Push(DreamValue.True);
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_Return(ref state.Proc, ref state.PC);
        state.RefreshSpans();
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleReturnFalse(ref InterpreterState state)
    {
        state.Push(DreamValue.False);
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_Return(ref state.Proc, ref state.PC);
        state.RefreshSpans();
        state.StackPtr = state.Thread._stackPtr;
    }

    private static void HandleLocalCompareNotEquals(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();

        var a = state.GetLocal(idx1);
        var b = state.GetLocal(idx2);
        state.Push(a != b ? DreamValue.True : DreamValue.False);
    }

    private static void HandleLocalIncrement(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        ref var val = ref state.GetLocal(idx);
        val = val + 1;
    }

    private static void HandleLocalDecrement(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        ref var val = ref state.GetLocal(idx);
        val = val - 1;
    }

    private static void HandleLocalPushLocalPushSub(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();

        var a = state.GetLocal(idx1);
        var b = state.GetLocal(idx2);

        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
        {
            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
                state.Push(new DreamValue(a.UnsafeRawLong - b.UnsafeRawLong));
            else
                state.Push(new DreamValue(a.UnsafeRawDouble - b.UnsafeRawDouble));
        }
        else
            state.Push(a - b);
    }

    private static void HandleLocalAddLocalAssign(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        ref var a = ref state.GetLocal(idx1);
        var b = state.GetLocal(idx2);
        a = a + b;
    }

    private static void HandleLocalSubLocalAssign(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        ref var a = ref state.GetLocal(idx1);
        var b = state.GetLocal(idx2);
        a = a - b;
    }

    private static void HandleLocalJumpIfNull(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        int address = state.ReadInt32();
        if (state.GetLocal(idx).IsNull) state.PC = address;
    }

    private static void HandleLocalJumpIfNotNull(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        int address = state.ReadInt32();
        if (!state.GetLocal(idx).IsNull) state.PC = address;
    }

    private static void HandleLocalCompareEqualsJumpIfFalse(ref InterpreterState state) => HandleLocalCompareJumpIfFalse(ref state, (a, b) => a == b);
    private static void HandleLocalCompareNotEqualsJumpIfFalse(ref InterpreterState state) => HandleLocalCompareJumpIfFalse(ref state, (a, b) => a != b);
    private static void HandleLocalCompareLessThanJumpIfFalse(ref InterpreterState state) => HandleLocalCompareJumpIfFalse(ref state, (a, b) => a < b);
    private static void HandleLocalCompareGreaterThanJumpIfFalse(ref InterpreterState state) => HandleLocalCompareJumpIfFalse(ref state, (a, b) => a > b);
    private static void HandleLocalCompareLessThanOrEqualJumpIfFalse(ref InterpreterState state) => HandleLocalCompareJumpIfFalse(ref state, (a, b) => a <= b);
    private static void HandleLocalCompareGreaterThanOrEqualJumpIfFalse(ref InterpreterState state) => HandleLocalCompareJumpIfFalse(ref state, (a, b) => a >= b);

    private static void HandleLocalPushDereferenceField(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        int nameId = state.ReadInt32();
        int pcForCache = state.PC - 5;
        var objValue = state.GetLocal(idx);
        DreamValue val = DreamValue.Null;
        if (objValue.TryGetValue(out DreamObject? obj) && obj != null)
        {
            ref var cache = ref state.Proc._inlineCache[pcForCache];
            if (cache.ObjectType == obj.ObjectType)
            {
                val = obj.GetVariableDirect(cache.VariableIndex);
            }
            else
            {
                var name = state.Strings[nameId];
                int varIdx = obj.ObjectType?.GetVariableIndex(name) ?? -1;
                if (varIdx != -1)
                {
                    cache.ObjectType = obj.ObjectType;
                    cache.VariableIndex = varIdx;
                    val = obj.GetVariableDirect(varIdx);
                }
                else val = obj.GetVariable(name);
            }
        }
        state.Push(val);
    }

    private static void HandleLocalMulLocalAssign(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        ref var a = ref state.GetLocal(idx1);
        var b = state.GetLocal(idx2);
        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
        {
            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
                a = new DreamValue(a.UnsafeRawLong * b.UnsafeRawLong);
            else
                a = new DreamValue(a.UnsafeRawDouble * b.UnsafeRawDouble);
        }
        else a = a * b;
    }

    private static void HandleLocalDivLocalAssign(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        ref var a = ref state.GetLocal(idx1);
        var b = state.GetLocal(idx2);
        double db = b.Type == DreamValueType.Float ? b.UnsafeRawDouble : b.GetValueAsDouble();
        if (db != 0)
        {
            if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
                a = new DreamValue(a.UnsafeRawDouble / db);
            else a = a / b;
        }
        else a = new DreamValue(0.0);
    }

    private static void HandleLocalMulFloatAssign(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        double val = state.ReadDouble();
        ref var a = ref state.GetLocal(idx);
        if (a.Type <= DreamValueType.Integer) a = new DreamValue(a.UnsafeRawDouble * val);
        else a = a * val;
    }

    private static void HandleLocalDivFloatAssign(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        double val = state.ReadDouble();
        ref var a = ref state.GetLocal(idx);
        if (val != 0)
        {
            if (a.Type <= DreamValueType.Integer) a = new DreamValue(a.UnsafeRawDouble / val);
            else a = a / val;
        }
        else a = new DreamValue(0.0);
    }

    private static void HandlePopN(ref InterpreterState state)
    {
        int count = state.ReadInt32();
        state.StackPtr -= count;
    }

    private static void HandleLocalMulFloat(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        double val = state.ReadDouble();
        var a = state.GetLocal(idx);
        if (a.Type <= DreamValueType.Integer) state.Push(new DreamValue(a.UnsafeRawDouble * val));
        else state.Push(a * val);
    }

    private static void HandleLocalDivFloat(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        double val = state.ReadDouble();
        var a = state.GetLocal(idx);
        if (val != 0)
        {
            if (a.Type <= DreamValueType.Integer) state.Push(new DreamValue(a.UnsafeRawDouble / val));
            else state.Push(a / val);
        }
        else state.Push(new DreamValue(0.0));
    }

    private static void HandleLocalPushLocalPushMul(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        var a = state.GetLocal(idx1);
        var b = state.GetLocal(idx2);
        if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
        {
            if (a.Type == DreamValueType.Integer && b.Type == DreamValueType.Integer)
                state.Push(new DreamValue(a.UnsafeRawLong * b.UnsafeRawLong));
            else
                state.Push(new DreamValue(a.UnsafeRawDouble * b.UnsafeRawDouble));
        }
        else state.Push(a * b);
    }

    private static void HandleLocalPushLocalPushDiv(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        var a = state.GetLocal(idx1);
        var b = state.GetLocal(idx2);
        double db = b.Type == DreamValueType.Float ? b.UnsafeRawDouble : b.GetValueAsDouble();
        if (db != 0)
        {
            if (a.Type <= DreamValueType.Integer && b.Type <= DreamValueType.Integer)
                state.Push(new DreamValue(a.UnsafeRawDouble / db));
            else
                state.Push(a / b);
        }
        else state.Push(new DreamValue(0.0));
    }

    private static void HandleLocalAddFloatAssign(ref InterpreterState state)
    {
        int idx = state.ReadInt32();
        double val = state.ReadDouble();
        ref var a = ref state.GetLocal(idx);
        if (a.Type <= DreamValueType.Integer) a = new DreamValue(a.UnsafeRawDouble + val);
        else a = a + val;
    }

    private static void HandleLocalCompareLessThan(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        state.Push(state.GetLocal(idx1) < state.GetLocal(idx2) ? DreamValue.True : DreamValue.False);
    }

    private static void HandleLocalCompareGreaterThan(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        state.Push(state.GetLocal(idx1) > state.GetLocal(idx2) ? DreamValue.True : DreamValue.False);
    }

    private static void HandleLocalCompareLessThanOrEqual(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        state.Push(state.GetLocal(idx1) <= state.GetLocal(idx2) ? DreamValue.True : DreamValue.False);
    }

    private static void HandleLocalCompareGreaterThanOrEqual(ref InterpreterState state)
    {
        int idx1 = state.ReadInt32();
        int idx2 = state.ReadInt32();
        state.Push(state.GetLocal(idx1) >= state.GetLocal(idx2) ? DreamValue.True : DreamValue.False);
    }

}
