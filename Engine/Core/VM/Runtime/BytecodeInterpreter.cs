using Shared.Enums;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
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
    private readonly IDiagnosticBus? _diagnosticBus;
    private long _lastReportTime;

    public BytecodeInterpreter(IDiagnosticBus? diagnosticBus = null)
    {
        _diagnosticBus = diagnosticBus;
    }

    [ThreadStatic]
    private static long _instructionsThisTick;
    [ThreadStatic]
    private static long _totalInstructions;
    [ThreadStatic]
    private static long[]? _opcodeFrequency;


    private void ReportTelemetry()
    {
        if (_diagnosticBus == null) return;

        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        var elapsed = (double)(now - _lastReportTime) / System.Diagnostics.Stopwatch.Frequency;

        if (elapsed >= 1.0) // Report every second
        {
            var ips = _instructionsThisTick / elapsed;
            _diagnosticBus.Publish("VM.Interpreter", "Performance Metrics", ips, (m, val) =>
            {
                m.Add("InstructionsPerSecond", val);
                m.Add("TotalInstructions", _totalInstructions);
            });

            _instructionsThisTick = 0;
            _lastReportTime = now;
        }
    }
}
