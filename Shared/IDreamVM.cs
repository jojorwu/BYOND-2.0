using System.Collections.Generic;

namespace Shared
{
    public interface IDreamVM
    {
        List<string> Strings { get; }
        IReadOnlyList<IDreamProc> ProcsById { get; }
        IReadOnlyDictionary<string, int> ProcNameIds { get; }
        IScriptThread? CreateThread(string procName, IGameObject? associatedObject = null);
    }
}
