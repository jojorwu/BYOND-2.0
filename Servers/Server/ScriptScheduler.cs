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

        public async System.Threading.Tasks.Task<IEnumerable<IScriptThread>> ExecuteThreadsAsync(IEnumerable<IScriptThread> threads, IEnumerable<IGameObject> objectsToTick, bool processGlobals = false, HashSet<int>? objectIds = null)
        {
            if (objectIds == null)
            {
                objectIds = new HashSet<int>();
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
                _jobSystem.Schedule(() =>
                {
                    if (Interlocked.CompareExchange(ref budgetExceeded, 0, 0) == 1 || (stopwatch.Elapsed.TotalMilliseconds >= budgetMs && _settings.Performance.TimeBudgeting.ScriptHost.Enabled))
                    {
                        Interlocked.Exchange(ref budgetExceeded, 1);
                        thread.WaitTicks++;
                        nextThreads.Add(thread);
                        return;
                    }

                    ProcessThread(thread, processGlobals, objectIds, nextThreads);
                });
            }

            await _jobSystem.CompleteAllAsync();

            return nextThreads;
        }

        private void ProcessThread(IScriptThread thread, bool processGlobals, HashSet<int>? objectIds, ConcurrentBag<IScriptThread> nextThreads)
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
                    var state = dreamThread.Run(_settings.Performance.VmInstructionSlice);
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
    }
}
