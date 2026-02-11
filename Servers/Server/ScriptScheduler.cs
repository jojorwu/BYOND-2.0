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

            var nextThreads = new ConcurrentBag<IScriptThread>();
            var budgetMs = 1000.0 / _settings.Performance.TickRate * _settings.Performance.TimeBudgeting.ScriptHost.BudgetPercent;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            Parallel.ForEach(threads, thread =>
            {
                if (thread is DreamThread dreamThread)
                {
                    if (stopwatch.Elapsed.TotalMilliseconds >= budgetMs && _settings.Performance.TimeBudgeting.ScriptHost.Enabled)
                    {
                        nextThreads.Add(dreamThread);
                        return;
                    }

                    bool shouldProcess = (processGlobals && dreamThread.AssociatedObject == null) || (dreamThread.AssociatedObject != null && objectIds.Contains(dreamThread.AssociatedObject.Id));

                    if (shouldProcess)
                    {
                        var state = dreamThread.Run(_settings.Performance.VmInstructionSlice);
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
