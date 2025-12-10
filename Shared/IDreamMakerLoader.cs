namespace Shared
{
    public interface IDreamMakerLoader
    {
        void Load(IPublicDreamCompiledJson compiledJson);
        IScriptThread? CreateThread(string procName);
    }
}
