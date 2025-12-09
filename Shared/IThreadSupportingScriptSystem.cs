namespace Shared
{
    public interface IThreadSupportingScriptSystem : IScriptSystem
    {
        IScriptThread? CreateThread(string procName);
    }
}
