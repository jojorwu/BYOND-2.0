namespace Shared
{
    public enum EngineComponent
    {
        Client,
        Server,
        Compiler,
        Editor
    }

    public interface IEngineManager
    {
        string GetExecutablePath(EngineComponent component);
        bool IsComponentInstalled(EngineComponent component);
        void InstallComponent(EngineComponent component); // Foundation for future
        string GetBaseEnginePath();
        void SetBaseEnginePath(string path);
        void LoadSettings();
        void SaveSettings();
    }
}
