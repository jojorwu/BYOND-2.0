using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Microsoft.Extensions.Options;
using Shared;
using Core.VM.Runtime;

namespace Server
{
    public class ScriptScheduler : IScriptScheduler
    {
        private readonly ServerSettings _settings;

        public ScriptScheduler(IOptions<ServerSettings> settings)
        {
            _settings = settings.Value;
        }

        public IEnumerable<IScriptThread> ExecuteThreads(IEnumerable<IScriptThread> threads, IEnumerable<IGameObject> objectsToTick, bool processGlobals = false, HashSet<int>? objectIds = null)
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

            Parallel.ForEach(sortedThreads, thread =>
            {
                if (Interlocked.CompareExchange(ref budgetExceeded, 0, 0) == 1 || (stopwatch.Elapsed.TotalMilliseconds >= budgetMs && _settings.Performance.TimeBudgeting.ScriptHost.Enabled))
                {
                    Interlocked.Exchange(ref budgetExceeded, 1);
                    thread.WaitTicks++;
                    nextThreads.Add(thread);
                    return;
                }

                if (thread is DreamThread dreamThread)
                {
                    bool shouldProcess = (processGlobals && dreamThread.AssociatedObject == null) || (dreamThread.AssociatedObject != null && objectIds.Contains(dreamThread.AssociatedObject.Id));

                    if (shouldProcess)
                    {
                        var threadStopwatch = System.Diagnostics.Stopwatch.StartNew();
                        var state = dreamThread.Run(_settings.Performance.VmInstructionSlice);
                        dreamThread.ExecutionTime = threadStopwatch.Elapsed;
                        dreamThread.WaitTicks = 0;

                        if (state == DreamThreadState.Running || state == DreamThreadState.Sleeping)
                        {
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
            });

            return nextThreads;
        }
    }
}
