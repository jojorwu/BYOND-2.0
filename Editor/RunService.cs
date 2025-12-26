using System;
using System.Diagnostics;
using System.IO;
using Launcher;

namespace Editor
{
    public class RunService : IRunService
    {
        private readonly IProcessService _processService;

        public RunService(IProcessService processService)
        {
            _processService = processService;
        }

        public void Run()
        {
            var serverExecutable = "Server.exe"; // TODO: Make this configurable
            var clientExecutable = "Client.exe"; // TODO: Make this configurable

            var serverPath = Path.Combine(AppContext.BaseDirectory, serverExecutable);
            var clientPath = Path.Combine(AppContext.BaseDirectory, clientExecutable);

            try
            {
                _processService.Start(serverPath);
                _processService.Start(clientPath);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to run project: {e.Message}");
                // TODO: Show a user-friendly error message
            }
        }
    }
}
