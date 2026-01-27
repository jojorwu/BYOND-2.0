using System.Collections.Generic;

namespace Shared
{
    public interface IDreamVM
    {
        List<string> Strings { get; }
        Dictionary<string, IDreamProc> Procs { get; }
        List<DreamValue> Globals { get; }
        ObjectType? ListType { get; set; }
        IScriptThread? CreateThread(string procName, IGameObject? associatedObject = null);
    }
}
