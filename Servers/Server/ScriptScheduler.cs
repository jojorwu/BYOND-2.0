using Shared.Enums;
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

        public ScriptScheduler(IOptions<ServerSettings> settings, ITimerService timerService, IJobSystem jobSystem)
        {
            _settings = settings.Value;
            _timerService = timerService;
            _jobSystem = jobSystem;
        }

        public async System.Threading.Tasks.Task<IEnumerable<IScriptThread>> ExecuteThreadsAsync(IEnumerable<IScriptThread> threads, IEnumerable<IGameObject> objectsToTick, bool processGlobals = false, HashSet<long>? objectIds = null, bool forceSequential = false)
        {
            if (objectIds == null)
            {
                objectIds = new HashSet<long>();
                foreach (var obj in objectsToTick)
                {
                    objectIds.Add(obj.Id);
                }
            }

            var nextThreads = new ConcurrentBag<IScriptThread>();
            var budgetMs = 1000.0 / _settings.Performance.TickRate * _settings.Performance.TimeBudgeting.ScriptHost.BudgetPercent;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Use faster manual sorting to avoid LINQ overhead for large thread counts
            var sortedThreads = threads as List<IScriptThread> ?? threads.ToList();
            sortedThreads.Sort((a, b) =>
            {
                int res = b.Priority.CompareTo(a.Priority);
                if (res != 0) return res;
                return b.WaitTicks.CompareTo(a.WaitTicks);
            });

            var budgetExceeded = 0; // 0 = false, 1 = true
            const int BatchSize = 64;

            if (forceSequential)
            {
                foreach (var thread in sortedThreads)
                {
                    if (Interlocked.CompareExchange(ref budgetExceeded, 0, 0) == 1 || (stopwatch.Elapsed.TotalMilliseconds >= budgetMs && _settings.Performance.TimeBudgeting.ScriptHost.Enabled))
                    {
                        Interlocked.Exchange(ref budgetExceeded, 1);
                        thread.WaitTicks++;
                        nextThreads.Add(thread);
                        continue;
                    }

                    int instructionSlice = CalculateInstructionSlice(thread);
                    ProcessThread(thread, processGlobals, objectIds, nextThreads, instructionSlice);
                }
            }
            else
            {
                // Parallel path: Process in batches to reduce JobSystem overhead
                for (int i = 0; i < sortedThreads.Count; i += BatchSize)
                {
                    int start = i;
                    int end = Math.Min(i + BatchSize, sortedThreads.Count);
                    var batch = sortedThreads.GetRange(start, end - start);

                    // Use priority of first thread in batch (they are sorted by priority)
                    var jobPriority = MapPriority(batch[0].Priority);

                    // Weight batch by instruction count of heaviest thread
                    int maxWeight = 1;
                    foreach (var t in batch)
                    {
                        if (t.TotalInstructionsExecuted > 500000) maxWeight = Math.Max(maxWeight, 10);
                        else if (t.TotalInstructionsExecuted > 100000) maxWeight = Math.Max(maxWeight, 5);
                        else if (t.TotalInstructionsExecuted > 10000) maxWeight = Math.Max(maxWeight, 2);
                    }

                    _jobSystem.Schedule(() =>
                    {
                        foreach (var thread in batch)
                        {
                            if (Interlocked.CompareExchange(ref budgetExceeded, 0, 0) == 1 || (stopwatch.Elapsed.TotalMilliseconds >= budgetMs && _settings.Performance.TimeBudgeting.ScriptHost.Enabled))
                            {
                                Interlocked.Exchange(ref budgetExceeded, 1);
                                thread.WaitTicks++;
                                nextThreads.Add(thread);
                                continue;
                            }

                            int instructionSlice = CalculateInstructionSlice(thread);
                            ProcessThread(thread, processGlobals, objectIds, nextThreads, instructionSlice);
                        }
                    }, priority: jobPriority, weight: maxWeight);
                }
            }

            if (!forceSequential)
                await _jobSystem.CompleteAllAsync();

            return nextThreads;
        }

        private int CalculateInstructionSlice(IScriptThread thread)
        {
            int instructionSlice = _settings.Performance.VmInstructionSlice;
            if (thread.Priority == ScriptThreadPriority.High) instructionSlice *= 2;
            else if (thread.Priority == ScriptThreadPriority.Low) instructionSlice /= 2;

            // Hot Path Detection: Scripts that are running heavy tasks get more airtime
            // to finish their work faster and reduce context-switch frequency.
            if (thread.TotalInstructionsExecuted > 1000000)
            {
                instructionSlice *= 4;
            }
            else if (thread.TotalInstructionsExecuted > 100000)
            {
                instructionSlice *= 2;
            }

            // Apply carry-over balance from previous ticks (rewarding yielding, penalizing over-consumption)
            instructionSlice += thread.InstructionQuotaBalance;
            return Math.Max(100, instructionSlice); // Minimum slice
        }

        private void ProcessThread(IScriptThread thread, bool processGlobals, HashSet<long>? objectIds, ConcurrentBag<IScriptThread> nextThreads, int instructionSlice)
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
