using System.Threading.Tasks;

namespace Shared.Interfaces;
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
        void InstallComponent(EngineComponent component);
        string GetBaseEnginePath();
        void SetBaseEnginePath(string path);
        void LoadSettings();
        void SaveSettings();
        Task InitializeAsync();
        Task ShutdownAsync();
    }
