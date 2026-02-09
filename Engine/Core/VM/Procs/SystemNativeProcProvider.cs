using System;
using System.Collections.Generic;
using Shared;
using Core.VM.Runtime;

namespace Core.VM.Procs
{
    public class SystemNativeProcProvider : INativeProcProvider
    {
        public IDictionary<string, IDreamProc> GetNativeProcs()
        {
            var procs = new Dictionary<string, IDreamProc>();

            procs["sleep"] = new NativeProc("sleep", (thread, src, args) =>
            {
                if (args.Length > 0 && args[0].TryGetValue(out float seconds))
                {
                    if (float.IsNaN(seconds) || float.IsInfinity(seconds)) seconds = 0;
                    seconds = Math.Clamp(seconds, 0, 864000); // Max 24 hours (864000 deciseconds)
                    thread.Sleep(seconds / 10.0f); // DM sleep is in deciseconds
                }
                return DreamValue.Null;
            });

            return procs;
        }
    }
}
