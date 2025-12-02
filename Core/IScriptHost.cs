using Core.VM.Runtime;

namespace Core
{
    public interface IScriptHost
    {
        void AddThread(DreamThread thread);
    }
}
