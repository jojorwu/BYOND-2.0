using System.Collections.Generic;

namespace Shared;
    /// <summary>
    /// Contributes native procedures to the VM.
    /// </summary>
    public interface INativeProcProvider
    {
        /// <summary>
        /// Returns a dictionary of procedure names mapped to their native implementations.
        /// </summary>
        IDictionary<string, IDreamProc> GetNativeProcs();
    }
