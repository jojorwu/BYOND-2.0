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
    private readonly DreamVM? _vm;
    private readonly IDiagnosticBus? _diagnosticBus;

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
    private void RecordInstruction(byte opcode)
    {
        var telemetry = _telemetry ??= new ThreadTelemetry { LastReportTime = System.Diagnostics.Stopwatch.GetTimestamp() };
        telemetry.InstructionsThisTick++;
        telemetry.TotalInstructions++;
        telemetry.OpcodeCounts[opcode]++;
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
