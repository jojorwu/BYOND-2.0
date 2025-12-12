namespace Shared
{
    public interface IScriptHost
    {
        void Initialize();
        void Tick();
        void EnqueueCommand(string command, Action<string> onResult);
        void AddThread(IScriptThread thread);
    }
}
