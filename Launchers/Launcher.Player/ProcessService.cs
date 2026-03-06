using System.Diagnostics;

namespace Launcher
{
    public interface IProcessService
    {
        void Start(string fileName, string? arguments = null);
    }

    public class ProcessService : IProcessService
    {
        public void Start(string fileName, string? arguments = null)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = fileName.EndsWith(".exe") || fileName.EndsWith(".sh") || fileName.EndsWith(".py") || fileName.EndsWith(".dll") ? false : true,
                CreateNoWindow = false
            };
            Process.Start(startInfo);
        }
    }
}
