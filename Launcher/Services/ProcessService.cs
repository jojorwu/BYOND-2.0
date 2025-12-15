using System;
using System.ComponentModel;
using System.Diagnostics;

namespace Launcher.Services
{
    public class ProcessService : IProcessService
    {
        public Action<string>? OnError { get; set; }

        public void StartProcess(string fileName, string? arguments = null)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments ?? string.Empty,
#if DEBUG
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
#else
                    UseShellExecute = true,
#endif
                };
                var process = Process.Start(startInfo);
            }
            catch (Win32Exception e)
            {
                OnError?.Invoke($"Error starting {fileName}:\n{e.Message}\n\nMake sure {fileName} is in the same directory as the launcher.");
            }
            catch (Exception e)
            {
                OnError?.Invoke($"An unexpected error occurred:\n{e.Message}");
            }
        }
    }
}
