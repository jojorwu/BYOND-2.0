using System.Collections.Generic;

namespace Shared
{
    public interface IDreamVM
    {
        List<string> Strings { get; }
        Dictionary<string, IDreamProc> Procs { get; }
        List<DreamValue> Globals { get; }
        IScriptThread? CreateThread(string procName, IGameObject? associatedObject = null);
    }
}
