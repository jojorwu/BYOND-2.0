using System.Collections.Generic;

namespace Shared
{
    public interface IDreamVM
    {
        List<string> Strings { get; }
        Dictionary<string, IDreamProc> Procs { get; }
        List<DreamValue> Globals { get; }
        ObjectType? ListType { get; set; }
        IObjectTypeManager? ObjectTypeManager { get; set; }
        IGameState? GameState { get; set; }
        IGameApi? GameApi { get; set; }
        void Initialize();
        IScriptThread? CreateThread(string procName, IGameObject? associatedObject = null);
    }
}
