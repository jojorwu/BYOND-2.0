using Shared.Enums;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Frozen;
using System.Linq;
using System.Runtime.CompilerServices;
using Core.VM.Procs;
using Shared;
using Shared.Interfaces;
using Shared.Services;

namespace Core.VM.Runtime;

/// <summary>
/// Defines the core contract for the Dream VM bytecode executor.
/// </summary>
public interface IBytecodeInterpreter
{
    /// <summary>
    /// Executes instructions from the thread's current state until the budget is exhausted
    /// or the thread enters a non-running state.
    /// </summary>
    DreamThreadState Run(DreamThread thread, int instructionBudget);
}


public unsafe partial class BytecodeInterpreter : EngineService, IBytecodeInterpreter
{
    private readonly DreamVM? _vm;
    private readonly IDiagnosticBus? _diagnosticBus;

    private static readonly delegate*<ref InterpreterState, void>[] _dispatchTable;

    static BytecodeInterpreter()
    {
        _dispatchTable = new delegate*<ref InterpreterState, void>[256];
        for (int i = 0; i < 256; i++) _dispatchTable[i] = &HandleUnknownOpcode;

        // Core Opcodes
        _dispatchTable[(byte)Opcode.BitShiftLeft] = &HandleBitShiftLeft;
        _dispatchTable[(byte)Opcode.PushType] = &HandlePushType;
        _dispatchTable[(byte)Opcode.PushString] = &HandlePushString;
        _dispatchTable[(byte)Opcode.FormatString] = &HandleFormatString;
        _dispatchTable[(byte)Opcode.SwitchCaseRange] = &HandleSwitchCaseRange;
        _dispatchTable[(byte)Opcode.PushReferenceValue] = &HandlePushReferenceValue;
        _dispatchTable[(byte)Opcode.Rgb] = &HandleRgb;
        _dispatchTable[(byte)Opcode.Add] = &HandleAdd;
        _dispatchTable[(byte)Opcode.Assign] = &HandleAssign;
        _dispatchTable[(byte)Opcode.Call] = &HandleCall;
        _dispatchTable[(byte)Opcode.MultiplyReference] = &HandleMultiplyReference;
        _dispatchTable[(byte)Opcode.JumpIfFalse] = &HandleJumpIfFalse;
        _dispatchTable[(byte)Opcode.CreateStrictAssociativeList] = &HandleCreateStrictAssociativeList;
        _dispatchTable[(byte)Opcode.Jump] = &HandleJump;
        _dispatchTable[(byte)Opcode.CompareEquals] = &HandleCompareEquals;
        _dispatchTable[(byte)Opcode.Return] = &HandleReturn;
        _dispatchTable[(byte)Opcode.PushNull] = &HandlePushNull;
        _dispatchTable[(byte)Opcode.Subtract] = &HandleSubtract;
        _dispatchTable[(byte)Opcode.CompareLessThan] = &HandleCompareLessThan;
        _dispatchTable[(byte)Opcode.CompareGreaterThan] = &HandleCompareGreaterThan;
        _dispatchTable[(byte)Opcode.BooleanAnd] = &HandleBooleanAnd;
        _dispatchTable[(byte)Opcode.BooleanNot] = &HandleBooleanNot;
        _dispatchTable[(byte)Opcode.DivideReference] = &HandleDivideReference;
        _dispatchTable[(byte)Opcode.Negate] = &HandleNegate;
        _dispatchTable[(byte)Opcode.Modulus] = &HandleModulus;
        _dispatchTable[(byte)Opcode.Append] = &HandleAppend;
        _dispatchTable[(byte)Opcode.CreateRangeEnumerator] = &HandleCreateRangeEnumerator;
        _dispatchTable[(byte)Opcode.Input] = &HandleInput;
        _dispatchTable[(byte)Opcode.CompareLessThanOrEqual] = &HandleCompareLessThanOrEqual;
        _dispatchTable[(byte)Opcode.CreateAssociativeList] = &HandleCreateAssociativeList;
        _dispatchTable[(byte)Opcode.Remove] = &HandleRemove;
        _dispatchTable[(byte)Opcode.DeleteObject] = &HandleDeleteObject;
        _dispatchTable[(byte)Opcode.PushResource] = &HandlePushResource;
        _dispatchTable[(byte)Opcode.CreateList] = &HandleCreateList;
        _dispatchTable[(byte)Opcode.CallStatement] = &HandleCallStatement;
        _dispatchTable[(byte)Opcode.BitAnd] = &HandleBitAnd;
        _dispatchTable[(byte)Opcode.CompareNotEquals] = &HandleCompareNotEquals;
        _dispatchTable[(byte)Opcode.PushProc] = &HandlePushProc;
        _dispatchTable[(byte)Opcode.Divide] = &HandleDivide;
        _dispatchTable[(byte)Opcode.Multiply] = &HandleMultiply;
        _dispatchTable[(byte)Opcode.BitXorReference] = &HandleBitXorReference;
        _dispatchTable[(byte)Opcode.BitXor] = &HandleBitXor;
        _dispatchTable[(byte)Opcode.BitOr] = &HandleBitOr;
        _dispatchTable[(byte)Opcode.BitNot] = &HandleBitNot;
        _dispatchTable[(byte)Opcode.Combine] = &HandleCombine;
        _dispatchTable[(byte)Opcode.CreateObject] = &HandleCreateObject;
        _dispatchTable[(byte)Opcode.BooleanOr] = &HandleBooleanOr;
        _dispatchTable[(byte)Opcode.CreateMultidimensionalList] = &HandleCreateMultidimensionalList;
        _dispatchTable[(byte)Opcode.CompareGreaterThanOrEqual] = &HandleCompareGreaterThanOrEqual;
        _dispatchTable[(byte)Opcode.SwitchCase] = &HandleSwitchCase;
        _dispatchTable[(byte)Opcode.Mask] = &HandleMask;
        _dispatchTable[(byte)Opcode.SetVariable] = &HandleSetVariable;
        _dispatchTable[(byte)Opcode.Error] = &HandleError;
        _dispatchTable[(byte)Opcode.IsInList] = &HandleIsInList;
        _dispatchTable[(byte)Opcode.PushFloat] = &HandlePushFloat;
        _dispatchTable[(byte)Opcode.ModulusReference] = &HandleModulusReference;
        _dispatchTable[(byte)Opcode.CreateListEnumerator] = &HandleCreateListEnumerator;
        _dispatchTable[(byte)Opcode.Enumerate] = &HandleEnumerate;
        _dispatchTable[(byte)Opcode.DestroyEnumerator] = &HandleDestroyEnumerator;
        _dispatchTable[(byte)Opcode.Browse] = &HandleBrowse;
        _dispatchTable[(byte)Opcode.BrowseResource] = &HandleBrowseResource;
        _dispatchTable[(byte)Opcode.OutputControl] = &HandleOutputControl;
        _dispatchTable[(byte)Opcode.BitShiftRight] = &HandleBitShiftRight;
        _dispatchTable[(byte)Opcode.CreateFilteredListEnumerator] = &HandleCreateFilteredListEnumerator;
        _dispatchTable[(byte)Opcode.Power] = &HandlePower;
        _dispatchTable[(byte)Opcode.EnumerateAssoc] = &HandleEnumerateAssoc;
        _dispatchTable[(byte)Opcode.Link] = &HandleLink;
        _dispatchTable[(byte)Opcode.Prompt] = &HandlePrompt;
        _dispatchTable[(byte)Opcode.Ftp] = &HandleFtp;
        _dispatchTable[(byte)Opcode.Initial] = &HandleInitial;
        _dispatchTable[(byte)Opcode.AsType] = &HandleAsType;
        _dispatchTable[(byte)Opcode.IsType] = &HandleIsType;
        _dispatchTable[(byte)Opcode.LocateCoord] = &HandleLocateCoord;
        _dispatchTable[(byte)Opcode.Locate] = &HandleLocate;
        _dispatchTable[(byte)Opcode.IsNull] = &HandleIsNull;
        _dispatchTable[(byte)Opcode.Spawn] = &HandleSpawn;
        _dispatchTable[(byte)Opcode.OutputReference] = &HandleOutputReference;
        _dispatchTable[(byte)Opcode.Output] = &HandleOutput;
        _dispatchTable[(byte)Opcode.GetVariable] = &HandleGetVariable;
        _dispatchTable[(byte)Opcode.Pop] = &HandlePop;
        _dispatchTable[(byte)Opcode.Prob] = &HandleProb;
        _dispatchTable[(byte)Opcode.IsSaved] = &HandleIsSaved;
        _dispatchTable[(byte)Opcode.PickUnweighted] = &HandlePickUnweighted;
        _dispatchTable[(byte)Opcode.PickWeighted] = &HandlePickWeighted;
        _dispatchTable[(byte)Opcode.Increment] = &HandleIncrement;
        _dispatchTable[(byte)Opcode.Decrement] = &HandleDecrement;
        _dispatchTable[(byte)Opcode.CompareEquivalent] = &HandleCompareEquivalent;
        _dispatchTable[(byte)Opcode.CompareNotEquivalent] = &HandleCompareNotEquivalent;
        _dispatchTable[(byte)Opcode.Throw] = &HandleThrow;
        _dispatchTable[(byte)Opcode.IsInRange] = &HandleIsInRange;
        _dispatchTable[(byte)Opcode.MassConcatenation] = &HandleMassConcatenation;
        _dispatchTable[(byte)Opcode.CreateTypeEnumerator] = &HandleCreateTypeEnumerator;
        _dispatchTable[(byte)Opcode.PushGlobalVars] = &HandlePushGlobalVars;
        _dispatchTable[(byte)Opcode.ModulusModulus] = &HandleModulusModulus;
        _dispatchTable[(byte)Opcode.ModulusModulusReference] = &HandleModulusModulusReference;
        _dispatchTable[(byte)Opcode.JumpIfNull] = &HandleJumpIfNull;
        _dispatchTable[(byte)Opcode.JumpIfNullNoPop] = &HandleJumpIfNullNoPop;
        _dispatchTable[(byte)Opcode.JumpIfTrueReference] = &HandleJumpIfTrueReference;
        _dispatchTable[(byte)Opcode.JumpIfFalseReference] = &HandleJumpIfFalseReference;
        _dispatchTable[(byte)Opcode.DereferenceField] = &HandleDereferenceField;
        _dispatchTable[(byte)Opcode.DereferenceIndex] = &HandleDereferenceIndex;
        _dispatchTable[(byte)Opcode.DereferenceCall] = &HandleDereferenceCall;
        _dispatchTable[(byte)Opcode.PopReference] = &HandlePopReference;
        _dispatchTable[(byte)Opcode.BitShiftLeftReference] = &HandleBitShiftLeftReference;
        _dispatchTable[(byte)Opcode.BitShiftRightReference] = &HandleBitShiftRightReference;
        _dispatchTable[(byte)Opcode.Try] = &HandleTry;
        _dispatchTable[(byte)Opcode.TryNoValue] = &HandleTryNoValue;
        _dispatchTable[(byte)Opcode.EndTry] = &HandleEndTry;
        _dispatchTable[(byte)Opcode.EnumerateNoAssign] = &HandleEnumerateNoAssign;
        _dispatchTable[(byte)Opcode.Gradient] = &HandleGradient;
        _dispatchTable[(byte)Opcode.AssignInto] = &HandleAssignInto;
        _dispatchTable[(byte)Opcode.GetStep] = &HandleGetStep;
        _dispatchTable[(byte)Opcode.Length] = &HandleLength;
        _dispatchTable[(byte)Opcode.GetDir] = &HandleGetDir;
        _dispatchTable[(byte)Opcode.DebuggerBreakpoint] = &HandleDebuggerBreakpoint;
        _dispatchTable[(byte)Opcode.Sin] = &HandleSin;
        _dispatchTable[(byte)Opcode.Cos] = &HandleCos;
        _dispatchTable[(byte)Opcode.Tan] = &HandleTan;
        _dispatchTable[(byte)Opcode.ArcSin] = &HandleArcSin;
        _dispatchTable[(byte)Opcode.ArcCos] = &HandleArcCos;
        _dispatchTable[(byte)Opcode.ArcTan] = &HandleArcTan;
        _dispatchTable[(byte)Opcode.ArcTan2] = &HandleArcTan2;
        _dispatchTable[(byte)Opcode.Sqrt] = &HandleSqrt;
        _dispatchTable[(byte)Opcode.Log] = &HandleLog;
        _dispatchTable[(byte)Opcode.LogE] = &HandleLogE;
        _dispatchTable[(byte)Opcode.Abs] = &HandleAbs;
        _dispatchTable[(byte)Opcode.AppendNoPush] = &HandleAppendNoPush;
        _dispatchTable[(byte)Opcode.AssignNoPush] = &HandleAssignNoPush;
        _dispatchTable[(byte)Opcode.PushRefAndDereferenceField] = &HandlePushRefAndDereferenceField;
        _dispatchTable[(byte)Opcode.PushNRefs] = &HandlePushNRefs;
        _dispatchTable[(byte)Opcode.PushNFloats] = &HandlePushNFloats;
        _dispatchTable[(byte)Opcode.PushNResources] = &HandlePushNResources;
        _dispatchTable[(byte)Opcode.PushStringFloat] = &HandlePushStringFloat;
        _dispatchTable[(byte)Opcode.JumpIfReferenceFalse] = &HandleJumpIfReferenceFalse;
        _dispatchTable[(byte)Opcode.PushNStrings] = &HandlePushNStrings;
        _dispatchTable[(byte)Opcode.SwitchOnFloat] = &HandleSwitchOnFloat;
        _dispatchTable[(byte)Opcode.PushNOfStringFloats] = &HandlePushNOfStringFloats;
        _dispatchTable[(byte)Opcode.CreateListNFloats] = &HandleCreateListNFloats;
        _dispatchTable[(byte)Opcode.CreateListNStrings] = &HandleCreateListNStrings;
        _dispatchTable[(byte)Opcode.CreateListNRefs] = &HandleCreateListNRefs;
        _dispatchTable[(byte)Opcode.CreateListNResources] = &HandleCreateListNResources;
        _dispatchTable[(byte)Opcode.SwitchOnString] = &HandleSwitchOnString;
        _dispatchTable[(byte)Opcode.IsTypeDirect] = &HandleIsTypeDirect;
        _dispatchTable[(byte)Opcode.NullRef] = &HandleNullRef;
        _dispatchTable[(byte)Opcode.ReturnReferenceValue] = &HandleReturnReferenceValue;
        _dispatchTable[(byte)Opcode.ReturnFloat] = &HandleReturnFloat;
        _dispatchTable[(byte)Opcode.IndexRefWithString] = &HandleIndexRefWithString;
        _dispatchTable[(byte)Opcode.PushFloatAssign] = &HandlePushFloatAssign;
        _dispatchTable[(byte)Opcode.NPushFloatAssign] = &HandleNPushFloatAssign;
        _dispatchTable[(byte)Opcode.PushLocal] = &HandlePushLocal;
        _dispatchTable[(byte)Opcode.GetStepTo] = &HandleGetStepTo;
        _dispatchTable[(byte)Opcode.GetDist] = &HandleGetDist;
        _dispatchTable[(byte)Opcode.PushArgument] = &HandlePushArgument;
        _dispatchTable[(byte)Opcode.AssignLocal] = &HandleAssignLocal;
        _dispatchTable[(byte)Opcode.LocalPushLocalPushAdd] = &HandleLocalPushLocalPushAdd;
        _dispatchTable[(byte)Opcode.LocalAddFloat] = &HandleLocalAddFloat;
        _dispatchTable[(byte)Opcode.LocalMulFloat] = &HandleLocalMulFloat;
        _dispatchTable[(byte)Opcode.LocalDivFloat] = &HandleLocalDivFloat;
        _dispatchTable[(byte)Opcode.LocalMulAdd] = &HandleLocalMulAdd;
        _dispatchTable[(byte)Opcode.GetBuiltinVar] = &HandleGetBuiltinVar;
        _dispatchTable[(byte)Opcode.SetBuiltinVar] = &HandleSetBuiltinVar;
        _dispatchTable[(byte)Opcode.LocalPushReturn] = &HandleLocalPushReturn;
        _dispatchTable[(byte)Opcode.LocalCompareEquals] = &HandleLocalCompareEquals;
        _dispatchTable[(byte)Opcode.LocalJumpIfFalse] = &HandleLocalJumpIfFalse;
        _dispatchTable[(byte)Opcode.LocalJumpIfTrue] = &HandleLocalJumpIfTrue;
        _dispatchTable[(byte)Opcode.ReturnNull] = &HandleReturnNull;
        _dispatchTable[(byte)Opcode.ReturnTrue] = &HandleReturnTrue;
        _dispatchTable[(byte)Opcode.ReturnFalse] = &HandleReturnFalse;
        _dispatchTable[(byte)Opcode.LocalCompareNotEquals] = &HandleLocalCompareNotEquals;
        _dispatchTable[(byte)Opcode.LocalIncrement] = &HandleLocalIncrement;
        _dispatchTable[(byte)Opcode.LocalDecrement] = &HandleLocalDecrement;
        _dispatchTable[(byte)Opcode.LocalPushLocalPushSub] = &HandleLocalPushLocalPushSub;
        _dispatchTable[(byte)Opcode.LocalPushLocalPushMul] = &HandleLocalPushLocalPushMul;
        _dispatchTable[(byte)Opcode.LocalPushLocalPushDiv] = &HandleLocalPushLocalPushDiv;
        _dispatchTable[(byte)Opcode.LocalAddLocalAssign] = &HandleLocalAddLocalAssign;
        _dispatchTable[(byte)Opcode.LocalSubLocalAssign] = &HandleLocalSubLocalAssign;
        _dispatchTable[(byte)Opcode.LocalJumpIfNull] = &HandleLocalJumpIfNull;
        _dispatchTable[(byte)Opcode.LocalJumpIfNotNull] = &HandleLocalJumpIfNotNull;
        _dispatchTable[(byte)Opcode.LocalCompareEqualsJumpIfFalse] = &HandleLocalCompareEqualsJumpIfFalse;
        _dispatchTable[(byte)Opcode.LocalCompareNotEqualsJumpIfFalse] = &HandleLocalCompareNotEqualsJumpIfFalse;
        _dispatchTable[(byte)Opcode.LocalAddFloatAssign] = &HandleLocalAddFloatAssign;
        _dispatchTable[(byte)Opcode.LocalCompareLessThan] = &HandleLocalCompareLessThan;
        _dispatchTable[(byte)Opcode.LocalCompareGreaterThan] = &HandleLocalCompareGreaterThan;
        _dispatchTable[(byte)Opcode.LocalCompareLessThanOrEqual] = &HandleLocalCompareLessThanOrEqual;
        _dispatchTable[(byte)Opcode.LocalCompareGreaterThanOrEqual] = &HandleLocalCompareGreaterThanOrEqual;
        _dispatchTable[(byte)Opcode.LocalMulLocalAssign] = &HandleLocalMulLocalAssign;
        _dispatchTable[(byte)Opcode.LocalDivLocalAssign] = &HandleLocalDivLocalAssign;
        _dispatchTable[(byte)Opcode.LocalMulFloatAssign] = &HandleLocalMulFloatAssign;
        _dispatchTable[(byte)Opcode.LocalDivFloatAssign] = &HandleLocalDivFloatAssign;
        _dispatchTable[(byte)Opcode.LocalCompareLessThanJumpIfFalse] = &HandleLocalCompareLessThanJumpIfFalse;
        _dispatchTable[(byte)Opcode.LocalCompareGreaterThanJumpIfFalse] = &HandleLocalCompareGreaterThanJumpIfFalse;
        _dispatchTable[(byte)Opcode.LocalCompareLessThanOrEqualJumpIfFalse] = &HandleLocalCompareLessThanOrEqualJumpIfFalse;
        _dispatchTable[(byte)Opcode.LocalCompareGreaterThanOrEqualJumpIfFalse] = &HandleLocalCompareGreaterThanOrEqualJumpIfFalse;
        _dispatchTable[(byte)Opcode.LocalPushDereferenceField] = &HandleLocalPushDereferenceField;
        _dispatchTable[(byte)Opcode.LocalPushDereferenceCall] = &HandleLocalPushDereferenceCall;
        _dispatchTable[(byte)Opcode.LocalPushDereferenceIndex] = &HandleLocalPushDereferenceIndex;
        _dispatchTable[(byte)Opcode.PopN] = &HandlePopN;
        _dispatchTable[(byte)Opcode.LocalCompareLessThanFloatJumpIfFalse] = &HandleLocalCompareLessThanFloatJumpIfFalse;
        _dispatchTable[(byte)Opcode.LocalCompareGreaterThanFloatJumpIfFalse] = &HandleLocalCompareGreaterThanFloatJumpIfFalse;
        _dispatchTable[(byte)Opcode.LocalCompareLessThanOrEqualFloatJumpIfFalse] = &HandleLocalCompareLessThanOrEqualFloatJumpIfFalse;
        _dispatchTable[(byte)Opcode.LocalCompareGreaterThanOrEqualFloatJumpIfFalse] = &HandleLocalCompareGreaterThanOrEqualFloatJumpIfFalse;
        _dispatchTable[(byte)Opcode.LocalJumpIfFieldFalse] = &HandleLocalJumpIfFieldFalse;
        _dispatchTable[(byte)Opcode.LocalJumpIfFieldTrue] = &HandleLocalJumpIfFieldTrue;
        _dispatchTable[(byte)Opcode.LocalFieldTransfer] = &HandleLocalFieldTransfer;
        _dispatchTable[(byte)Opcode.GlobalJumpIfFalse] = &HandleGlobalJumpIfFalse;
        _dispatchTable[(byte)Opcode.LocalPushDereferenceFieldJumpIfFalse] = &HandleLocalPushDereferenceFieldJumpIfFalse;
        _dispatchTable[(byte)Opcode.LocalAddConst] = &HandleLocalAddConst;
        _dispatchTable[(byte)Opcode.LocalSubConst] = &HandleLocalSubConst;

        // Specialized opcodes
        _dispatchTable[(byte)Opcode.PushLocal0] = &HandlePushLocal0;
        _dispatchTable[(byte)Opcode.PushLocal1] = &HandlePushLocal1;
        _dispatchTable[(byte)Opcode.PushLocal2] = &HandlePushLocal2;
        _dispatchTable[(byte)Opcode.PushLocal3] = &HandlePushLocal3;
        _dispatchTable[(byte)Opcode.PushLocal4] = &HandlePushLocal4;
        _dispatchTable[(byte)Opcode.PushLocal5] = &HandlePushLocal5;
        _dispatchTable[(byte)Opcode.PushLocal6] = &HandlePushLocal6;
        _dispatchTable[(byte)Opcode.PushLocal7] = &HandlePushLocal7;
        _dispatchTable[(byte)Opcode.PushLocal8] = &HandlePushLocal8;
        _dispatchTable[(byte)Opcode.PushLocal9] = &HandlePushLocal9;
        _dispatchTable[(byte)Opcode.PushLocal10] = &HandlePushLocal10;
        _dispatchTable[(byte)Opcode.PushLocal11] = &HandlePushLocal11;
        _dispatchTable[(byte)Opcode.PushLocal12] = &HandlePushLocal12;
        _dispatchTable[(byte)Opcode.PushLocal13] = &HandlePushLocal13;
        _dispatchTable[(byte)Opcode.PushLocal14] = &HandlePushLocal14;
        _dispatchTable[(byte)Opcode.PushLocal15] = &HandlePushLocal15;
        _dispatchTable[(byte)Opcode.AssignLocal0] = &HandleAssignLocal0;
        _dispatchTable[(byte)Opcode.AssignLocal1] = &HandleAssignLocal1;
        _dispatchTable[(byte)Opcode.AssignLocal2] = &HandleAssignLocal2;
        _dispatchTable[(byte)Opcode.AssignLocal3] = &HandleAssignLocal3;
        _dispatchTable[(byte)Opcode.AssignLocal4] = &HandleAssignLocal4;
        _dispatchTable[(byte)Opcode.AssignLocal5] = &HandleAssignLocal5;
        _dispatchTable[(byte)Opcode.AssignLocal6] = &HandleAssignLocal6;
        _dispatchTable[(byte)Opcode.AssignLocal7] = &HandleAssignLocal7;
        _dispatchTable[(byte)Opcode.AssignLocal8] = &HandleAssignLocal8;
        _dispatchTable[(byte)Opcode.AssignLocal9] = &HandleAssignLocal9;
        _dispatchTable[(byte)Opcode.AssignLocal10] = &HandleAssignLocal10;
        _dispatchTable[(byte)Opcode.AssignLocal11] = &HandleAssignLocal11;
        _dispatchTable[(byte)Opcode.AssignLocal12] = &HandleAssignLocal12;
        _dispatchTable[(byte)Opcode.AssignLocal13] = &HandleAssignLocal13;
        _dispatchTable[(byte)Opcode.AssignLocal14] = &HandleAssignLocal14;
        _dispatchTable[(byte)Opcode.AssignLocal15] = &HandleAssignLocal15;

        _dispatchTable[(byte)Opcode.CallGlobalProc] = &HandleCallGlobalProc;
    }

    private static void HandleUnknownOpcode(ref InterpreterState state)
    {
        throw new ScriptRuntimeException($"Unknown opcode: 0x{state.BytecodePtr[state.PC - 1]:X2}", state.Proc, state.PC, state.Thread);
    }

    private class ThreadTelemetry
    {
        public long LastReportTime;
        public long InstructionsThisTick;
        public long TotalInstructions;
        public readonly long[] OpcodeCounts = new long[256];
    }

    [ThreadStatic]
    private static ThreadTelemetry? _telemetry;

    public BytecodeInterpreter(IDiagnosticBus? diagnosticBus = null, DreamVM? vm = null)
    {
        _diagnosticBus = diagnosticBus;
        _vm = vm;
    }

    private void ReportTelemetry()
    {
        if (_diagnosticBus == null) return;

        var telemetry = _telemetry ??= new ThreadTelemetry { LastReportTime = System.Diagnostics.Stopwatch.GetTimestamp() };
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        var elapsed = (double)(now - telemetry.LastReportTime) / System.Diagnostics.Stopwatch.Frequency;

        if (elapsed >= 1.0) // Report every second per thread
        {
            var ips = telemetry.InstructionsThisTick / elapsed;
            _diagnosticBus.Publish("VM.Interpreter", $"Performance Metrics (Thread {Environment.CurrentManagedThreadId})", ips, (m, val) =>
            {
                m.Add("InstructionsPerSecond", val);
                m.Add("TotalInstructions", telemetry.TotalInstructions);
            });

            ReportHotPaths(telemetry);

            telemetry.InstructionsThisTick = 0;
            telemetry.LastReportTime = now;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordInstructions(int count)
    {
        var telemetry = _telemetry ??= new ThreadTelemetry { LastReportTime = System.Diagnostics.Stopwatch.GetTimestamp() };
        telemetry.InstructionsThisTick += count;
        telemetry.TotalInstructions += count;
    }

    private void ReportHotPaths(ThreadTelemetry telemetry)
    {
        if (_diagnosticBus == null) return;

        // Find top 5 opcodes
        var topOpcodes = Enumerable.Range(0, 256)
            .Select(i => (Opcode: (Opcode)i, Count: telemetry.OpcodeCounts[i]))
            .Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToList();

        _diagnosticBus.Publish("VM.HotPaths", $"Top Opcodes (Thread {Environment.CurrentManagedThreadId})", topOpcodes, (m, paths) =>
        {
            foreach (var path in paths)
            {
                m.Add(path.Opcode.ToString(), path.Count);
            }
        }, tags: new[] { "performance", "vm" });

        // Reset counts for next interval
        Array.Clear(telemetry.OpcodeCounts, 0, 256);
    }
}
