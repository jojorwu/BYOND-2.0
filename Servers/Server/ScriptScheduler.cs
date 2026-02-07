using System.Collections.Concurrent;
using System.Collections.Generic;
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

            var dreamThreads = threads.OfType<DreamThread>().ToList();
            var nextThreads = new ConcurrentBag<IScriptThread>();
            var budgetMs = 1000.0 / _settings.Performance.TickRate * _settings.Performance.TimeBudgeting.ScriptHost.BudgetPercent;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            foreach (var thread in dreamThreads)
            {
                if (stopwatch.Elapsed.TotalMilliseconds >= budgetMs && _settings.Performance.TimeBudgeting.ScriptHost.Enabled)
                {
                    nextThreads.Add(thread);
                    continue;
                }

                bool shouldProcess = (processGlobals && thread.AssociatedObject == null) || (thread.AssociatedObject != null && objectIds.Contains(thread.AssociatedObject.Id));

                if (shouldProcess)
                {
                    var state = thread.Run(_settings.Performance.VmInstructionSlice);
                    if (state == DreamThreadState.Running || state == DreamThreadState.Sleeping)
                    {
                        nextThreads.Add(thread);
                    }
                }
                else
                {
                    nextThreads.Add(thread);
                }
            }

            foreach (var thread in threads.Where(t => t is not DreamThread))
            {
                nextThreads.Add(thread);
            }

            return nextThreads;
        }
    }
}
