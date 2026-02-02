using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;

namespace Shared.Interfaces
{
    public interface IDreamVM
    {
        List<string> Strings { get; }
        Dictionary<string, IDreamProc> Procs { get; }
        List<DreamValue> Globals { get; }
        ObjectType? ListType { get; set; }
        IObjectTypeManager? ObjectTypeManager { get; set; }
        IGameState? GameState { get; set; }
        IScriptThread? CreateThread(string procName, IGameObject? associatedObject = null);
    }
}
