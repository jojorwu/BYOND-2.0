using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
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
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
    }
}
