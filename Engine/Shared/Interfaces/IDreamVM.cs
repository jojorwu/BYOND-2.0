using System.Collections.Generic;

namespace Shared;
    public interface IDreamVM
    {
        List<string> Strings { get; }
        System.Collections.Concurrent.ConcurrentDictionary<string, IDreamProc> Procs { get; }
        List<IDreamProc> AllProcs { get; }
        IList<DreamValue> Globals { get; }
        System.Collections.Concurrent.ConcurrentDictionary<string, int> GlobalNames { get; }
        ObjectType? ListType { get; set; }
        DreamObject? World { get; set; }
        IObjectTypeManager? ObjectTypeManager { get; set; }
        IGameState? GameState { get; set; }
        IGameApi? GameApi { get; set; }
        void Initialize();
        void Reset();
        IScriptThread? CreateThread(string procName, IGameObject? associatedObject = null);
    }
