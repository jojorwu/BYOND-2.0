using System.Collections.Generic;
using Shared;

namespace Core.VM.Procs
{
    /// <summary>
    /// A legacy provider that combines all standard native procedure providers.
    /// Useful for tests or simple setups where all procs are needed at once.
    /// </summary>
    public class StandardNativeProcProvider : INativeProcProvider
    {
        public IDictionary<string, IDreamProc> GetNativeProcs()
        {
            var procs = new Dictionary<string, IDreamProc>();
            var providers = new INativeProcProvider[]
            {
                new MathNativeProcProvider(),
                new SpatialNativeProcProvider(),
                new SystemNativeProcProvider()
            };

            foreach (var provider in providers)
            {
                foreach (var kvp in provider.GetNativeProcs())
                {
                    procs[kvp.Key] = kvp.Value;
                }
            }
            return procs;
        }
    }
}
