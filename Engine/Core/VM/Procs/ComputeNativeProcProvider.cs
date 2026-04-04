using System;
using System.Collections.Generic;
using Shared;
using Shared.Interfaces;
using Core.VM.Runtime;
using Robust.Shared.Maths;

namespace Core.VM.Procs
{
    public class ComputeNativeProcProvider : INativeProcProvider
    {
        private readonly IPathfindingService _pathfinding;
        private readonly IJobSystem _jobSystem;

        public ComputeNativeProcProvider(IPathfindingService pathfinding, IJobSystem jobSystem)
        {
            _pathfinding = pathfinding;
            _jobSystem = jobSystem;
        }

        public IDictionary<string, IDreamProc> GetNativeProcs()
        {
            var procs = new Dictionary<string, IDreamProc>();
            procs["/world/proc/pathfind_async"] = new NativeProc("pathfind_async", PathfindAsync);
            procs["/world/proc/run_job"] = new NativeProc("run_job", RunJob);
            return procs;
        }

        private DreamValue PathfindAsync(DreamThread thread, DreamObject? src, ReadOnlySpan<DreamValue> arguments)
        {
            if (arguments.Length < 2) return DreamValue.Null;

            var start = GetVector(arguments[0]);
            var end = GetVector(arguments[1]);
            int maxDepth = arguments.Length > 2 ? (int)arguments[2].RawLong : 1000;

            var task = _pathfinding.FindPathAsync(start, end, maxDepth);
            thread.SuspendUntil(task);
            return DreamValue.Null;
        }

        private DreamValue RunJob(DreamThread thread, DreamObject? src, ReadOnlySpan<DreamValue> arguments)
        {
            return DreamValue.Null;
        }

        private Vector3l GetVector(DreamValue val)
        {
            if (val.TryGetValueAsGameObject(out var obj)) return obj.Position;
            return Vector3l.Zero;
        }
    }
}
