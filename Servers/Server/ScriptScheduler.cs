using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Microsoft.Extensions.Options;
using Shared;
using Shared.Interfaces;
using Core.VM.Runtime;

namespace Server
{
    public class ScriptScheduler : IScriptScheduler
    {
        private readonly ServerSettings _settings;
        private readonly ITimerService _timerService;
        private readonly IJobSystem _jobSystem;
        private static readonly ThreadLocal<HashSet<int>> _objectIdBuffer = new(() => new HashSet<int>());

        public ScriptScheduler(IOptions<ServerSettings> settings, ITimerService timerService, IJobSystem jobSystem)
        {
            _settings = settings.Value;
            _timerService = timerService;
            _jobSystem = jobSystem;
        }

        public async System.Threading.Tasks.Task<IEnumerable<IScriptThread>> ExecuteThreadsAsync(IEnumerable<IScriptThread> threads, IEnumerable<IGameObject> objectsToTick, bool processGlobals = false, HashSet<int>? objectIds = null)
        {
            if (objectIds == null)
            {
                objectIds = _objectIdBuffer.Value!;
                objectIds.Clear();
                foreach (var obj in objectsToTick)
                {
                    objectIds.Add(obj.Id);
                }
            }

            var nextThreads = new ConcurrentBag<IScriptThread>();
            var budgetMs = 1000.0 / _settings.Performance.TickRate * _settings.Performance.TimeBudgeting.ScriptHost.BudgetPercent;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var sortedThreads = threads
                .OrderByDescending(t => t.Priority)
                .ThenByDescending(t => t.WaitTicks)
                .ToList();

            var budgetExceeded = 0; // 0 = false, 1 = true

            foreach (var thread in sortedThreads)
            {
                var jobPriority = MapPriority(thread.Priority);

                // Smart Boost: If a thread is heavy and has been waiting, give it a temporary boost
                if (thread.TotalInstructionsExecuted > 100000 && thread.WaitTicks > 5)
                {
                    jobPriority = JobPriority.High;
                }

                // Adaptive Weighting: Heavier scripts get more weight in the job system
                // to balance them against many small jobs.
                int jobWeight = 1;
                if (thread.TotalInstructionsExecuted > 500000) jobWeight = 10;
                else if (thread.TotalInstructionsExecuted > 100000) jobWeight = 5;
                else if (thread.TotalInstructionsExecuted > 10000) jobWeight = 2;

                _jobSystem.Schedule(() =>
                {
                    if (Interlocked.CompareExchange(ref budgetExceeded, 0, 0) == 1 || (stopwatch.Elapsed.TotalMilliseconds >= budgetMs && _settings.Performance.TimeBudgeting.ScriptHost.Enabled))
                    {
                        Interlocked.Exchange(ref budgetExceeded, 1);
                        thread.WaitTicks++;
                        nextThreads.Add(thread);
                        return;
                    }

                    // Calculate adaptive instruction slice based on priority
                    int instructionSlice = _settings.Performance.VmInstructionSlice;
                    if (thread.Priority == ScriptThreadPriority.High) instructionSlice *= 2;
                    else if (thread.Priority == ScriptThreadPriority.Low) instructionSlice /= 2;

                    // Apply carry-over balance from previous ticks (rewarding yielding, penalizing over-consumption)
                    instructionSlice += thread.InstructionQuotaBalance;
                    instructionSlice = Math.Max(100, instructionSlice); // Minimum slice

                    ProcessThread(thread, processGlobals, objectIds, nextThreads, instructionSlice);
                }, priority: jobPriority, weight: jobWeight);
            }

            await _jobSystem.CompleteAllAsync();

            return nextThreads;
        }

        private void ProcessThread(IScriptThread thread, bool processGlobals, HashSet<int>? objectIds, ConcurrentBag<IScriptThread> nextThreads, int instructionSlice)
        {
            if (thread is DreamThread dreamThread)
                {
                    // Skip sleeping threads in the main loop; TimerService will wake them up
                    if (dreamThread.State == DreamThreadState.Sleeping)
                    {
                        nextThreads.Add(dreamThread);
                        return;
                    }

                    bool shouldProcess = (processGlobals && dreamThread.AssociatedObject == null) || (dreamThread.AssociatedObject != null && objectIds!.Contains(dreamThread.AssociatedObject.Id));

                if (shouldProcess)
                {
                    var threadStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    long before = dreamThread.TotalInstructionsExecuted;

                    var state = dreamThread.Run(instructionSlice);

                    long executed = dreamThread.TotalInstructionsExecuted - before;
                    dreamThread.InstructionQuotaBalance = Math.Clamp(dreamThread.InstructionQuotaBalance + (int)(instructionSlice - executed), -50000, 50000);

                    dreamThread.ExecutionTime = threadStopwatch.Elapsed;
                    dreamThread.WaitTicks = 0;

                    if (state == DreamThreadState.Running)
                    {
                        nextThreads.Add(dreamThread);
                    }
                    else if (state == DreamThreadState.Sleeping)
                    {
                        // Register wakeup timer
                        _timerService.AddTimer(dreamThread.SleepUntil, dreamThread.WakeUp);
                        nextThreads.Add(dreamThread);
                    }
                    else
                    {
                        dreamThread.Dispose();
                    }
                }
                else
                {
                    nextThreads.Add(dreamThread);
                }
            }
            else
            {
                nextThreads.Add(thread);
            }
        }

        private JobPriority MapPriority(ScriptThreadPriority priority)
        {
            return priority switch
            {
                ScriptThreadPriority.High => JobPriority.High,
                ScriptThreadPriority.Normal => JobPriority.Normal,
                ScriptThreadPriority.Low => JobPriority.Low,
                _ => JobPriority.Normal
            };
        }
    }
}
