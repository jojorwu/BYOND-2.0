using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shared.Interfaces
{
    public interface IScriptManager
    {
        Task Initialize();
        Task ReloadAll();
        void InvokeGlobalEvent(string eventName);
        string? ExecuteCommand(string command);
        IScriptThread? CreateThread(string procName, IGameObject? associatedObject = null);
    }
}
