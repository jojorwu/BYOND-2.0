using System.Collections.Generic;

namespace Shared
{
    public interface IDreamVM
    {
        List<string> Strings { get; }
        Dictionary<string, IDreamProc> Procs { get; }
    }
}
