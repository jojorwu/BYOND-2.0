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

        table[(byte)Opcode.PushLocal0] = &HandlePushLocal0;
        table[(byte)Opcode.PushLocal1] = &HandlePushLocal1;
        table[(byte)Opcode.PushLocal2] = &HandlePushLocal2;
        table[(byte)Opcode.PushLocal3] = &HandlePushLocal3;
        table[(byte)Opcode.PushLocal4] = &HandlePushLocal4;
        table[(byte)Opcode.PushLocal5] = &HandlePushLocal5;
        table[(byte)Opcode.PushLocal6] = &HandlePushLocal6;
        table[(byte)Opcode.PushLocal7] = &HandlePushLocal7;
        table[(byte)Opcode.PushLocal8] = &HandlePushLocal8;
        table[(byte)Opcode.PushLocal9] = &HandlePushLocal9;
        table[(byte)Opcode.PushLocal10] = &HandlePushLocal10;
        table[(byte)Opcode.PushLocal11] = &HandlePushLocal11;
        table[(byte)Opcode.PushLocal12] = &HandlePushLocal12;
        table[(byte)Opcode.PushLocal13] = &HandlePushLocal13;
        table[(byte)Opcode.PushLocal14] = &HandlePushLocal14;
        table[(byte)Opcode.PushLocal15] = &HandlePushLocal15;

        table[(byte)Opcode.AssignLocal0] = &HandleAssignLocal0;
        table[(byte)Opcode.AssignLocal1] = &HandleAssignLocal1;
        table[(byte)Opcode.AssignLocal2] = &HandleAssignLocal2;
        table[(byte)Opcode.AssignLocal3] = &HandleAssignLocal3;
        table[(byte)Opcode.AssignLocal4] = &HandleAssignLocal4;
        table[(byte)Opcode.AssignLocal5] = &HandleAssignLocal5;
        table[(byte)Opcode.AssignLocal6] = &HandleAssignLocal6;
        table[(byte)Opcode.AssignLocal7] = &HandleAssignLocal7;
        table[(byte)Opcode.AssignLocal8] = &HandleAssignLocal8;
        table[(byte)Opcode.AssignLocal9] = &HandleAssignLocal9;
        table[(byte)Opcode.AssignLocal10] = &HandleAssignLocal10;
        table[(byte)Opcode.AssignLocal11] = &HandleAssignLocal11;
        table[(byte)Opcode.AssignLocal12] = &HandleAssignLocal12;
        table[(byte)Opcode.AssignLocal13] = &HandleAssignLocal13;
        table[(byte)Opcode.AssignLocal14] = &HandleAssignLocal14;
        table[(byte)Opcode.AssignLocal15] = &HandleAssignLocal15;

        return table;
    }

    private static void HandleUnknownOpcode(ref InterpreterState state)
    {
        throw new ScriptRuntimeException($"Unknown opcode: 0x{(byte)state.BytecodePtr[state.PC - 1]:X2}", state.Proc, state.PC - 1, state.Thread);
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

    private static void HandlePushGlobalVars(ref InterpreterState state)
    {
        state.Thread._stackPtr = state.StackPtr;
        state.Thread.Opcode_PushGlobalVars();
        state.StackPtr = state.Thread._stackPtr;
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

    private static readonly ThreadLocal<System.Text.StringBuilder> _formatStringBuilder = new(() => new System.Text.StringBuilder(256));

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

    private static void HandlePushLocal(ref InterpreterState state)
    {
        int idx = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;
        if ((uint)idx >= (uint)state.Proc.LocalVariableCount) throw new ScriptRuntimeException("Local index out of bounds", state.Proc, state.PC, state.Thread);
        state.Push(state.Stack[state.LocalBase + idx]);
    }

    private static void HandlePushLocal0(ref InterpreterState state) => state.Push(state.Stack[state.LocalBase + 0]);
    private static void HandlePushLocal1(ref InterpreterState state) => state.Push(state.Stack[state.LocalBase + 1]);
    private static void HandlePushLocal2(ref InterpreterState state) => state.Push(state.Stack[state.LocalBase + 2]);
    private static void HandlePushLocal3(ref InterpreterState state) => state.Push(state.Stack[state.LocalBase + 3]);
    private static void HandlePushLocal4(ref InterpreterState state) => state.Push(state.Stack[state.LocalBase + 4]);
    private static void HandlePushLocal5(ref InterpreterState state) => state.Push(state.Stack[state.LocalBase + 5]);
    private static void HandlePushLocal6(ref InterpreterState state) => state.Push(state.Stack[state.LocalBase + 6]);
    private static void HandlePushLocal7(ref InterpreterState state) => state.Push(state.Stack[state.LocalBase + 7]);
    private static void HandlePushLocal8(ref InterpreterState state) => state.Push(state.Stack[state.LocalBase + 8]);
    private static void HandlePushLocal9(ref InterpreterState state) => state.Push(state.Stack[state.LocalBase + 9]);
    private static void HandlePushLocal10(ref InterpreterState state) => state.Push(state.Stack[state.LocalBase + 10]);
    private static void HandlePushLocal11(ref InterpreterState state) => state.Push(state.Stack[state.LocalBase + 11]);
    private static void HandlePushLocal12(ref InterpreterState state) => state.Push(state.Stack[state.LocalBase + 12]);
    private static void HandlePushLocal13(ref InterpreterState state) => state.Push(state.Stack[state.LocalBase + 13]);
    private static void HandlePushLocal14(ref InterpreterState state) => state.Push(state.Stack[state.LocalBase + 14]);
    private static void HandlePushLocal15(ref InterpreterState state) => state.Push(state.Stack[state.LocalBase + 15]);

    private static void HandleAssignLocal0(ref InterpreterState state) => state.Stack[state.LocalBase + 0] = state.Stack[state.StackPtr - 1];
    private static void HandleAssignLocal1(ref InterpreterState state) => state.Stack[state.LocalBase + 1] = state.Stack[state.StackPtr - 1];
    private static void HandleAssignLocal2(ref InterpreterState state) => state.Stack[state.LocalBase + 2] = state.Stack[state.StackPtr - 1];
    private static void HandleAssignLocal3(ref InterpreterState state) => state.Stack[state.LocalBase + 3] = state.Stack[state.StackPtr - 1];
    private static void HandleAssignLocal4(ref InterpreterState state) => state.Stack[state.LocalBase + 4] = state.Stack[state.StackPtr - 1];
    private static void HandleAssignLocal5(ref InterpreterState state) => state.Stack[state.LocalBase + 5] = state.Stack[state.StackPtr - 1];
    private static void HandleAssignLocal6(ref InterpreterState state) => state.Stack[state.LocalBase + 6] = state.Stack[state.StackPtr - 1];
    private static void HandleAssignLocal7(ref InterpreterState state) => state.Stack[state.LocalBase + 7] = state.Stack[state.StackPtr - 1];
    private static void HandleAssignLocal8(ref InterpreterState state) => state.Stack[state.LocalBase + 8] = state.Stack[state.StackPtr - 1];
    private static void HandleAssignLocal9(ref InterpreterState state) => state.Stack[state.LocalBase + 9] = state.Stack[state.StackPtr - 1];
    private static void HandleAssignLocal10(ref InterpreterState state) => state.Stack[state.LocalBase + 10] = state.Stack[state.StackPtr - 1];
    private static void HandleAssignLocal11(ref InterpreterState state) => state.Stack[state.LocalBase + 11] = state.Stack[state.StackPtr - 1];
    private static void HandleAssignLocal12(ref InterpreterState state) => state.Stack[state.LocalBase + 12] = state.Stack[state.StackPtr - 1];
    private static void HandleAssignLocal13(ref InterpreterState state) => state.Stack[state.LocalBase + 13] = state.Stack[state.StackPtr - 1];
    private static void HandleAssignLocal14(ref InterpreterState state) => state.Stack[state.LocalBase + 14] = state.Stack[state.StackPtr - 1];
    private static void HandleAssignLocal15(ref InterpreterState state) => state.Stack[state.LocalBase + 15] = state.Stack[state.StackPtr - 1];

    private static void HandlePushArgument(ref InterpreterState state)
    {
        int idx = *(int*)(state.BytecodePtr + state.PC);
        state.PC += 4;
        if ((uint)idx >= (uint)state.Proc.Arguments.Length) throw new ScriptRuntimeException("Argument index out of bounds", state.Proc, state.PC, state.Thread);
        state.Push(state.Stack[state.ArgumentBase + idx]);
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

}
