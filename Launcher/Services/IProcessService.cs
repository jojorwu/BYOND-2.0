namespace Launcher.Services
{
    public interface IProcessService
    {
        void StartProcess(string fileName, string? arguments = null);
    }
}
