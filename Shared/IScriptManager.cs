using System.Threading.Tasks;

namespace Shared
{
    public interface IScriptManager
    {
        Task Initialize();
        Task ReloadAll();
        void InvokeGlobalEvent(string eventName);
        string? ExecuteCommand(string command);
        IScriptThread? CreateThread(string procName);
    }
}
