using Launcher.Services;

namespace Launcher
{
    class Program
    {
        static void Main(string[] args)
        {
            var processService = new ProcessService();
            var launcher = new Launcher(processService);
            launcher.Run();
        }
    }
}
