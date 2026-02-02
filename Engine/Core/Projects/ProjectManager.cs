using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;

namespace Core.Projects
{
    public class ProjectManager : IProjectManager
    {
        private readonly ILogger<ProjectManager> _logger;

        public ProjectManager(ILogger<ProjectManager> logger)
        {
            _logger = logger;
        }

        public async Task<bool> CreateProjectAsync(string projectName, string projectPath)
        {
            try
            {
                var fullProjectPath = Path.Combine(projectPath, projectName);
                if (Directory.Exists(fullProjectPath))
                {
                    _logger.LogError("Directory already exists: {Path}", fullProjectPath);
                    return false;
                }

                _logger.LogInformation("Creating project '{ProjectName}' at '{Path}'", projectName, fullProjectPath);

                // Create directories
                Directory.CreateDirectory(fullProjectPath);
                Directory.CreateDirectory(Path.Combine(fullProjectPath, "maps"));
                Directory.CreateDirectory(Path.Combine(fullProjectPath, "code"));
                Directory.CreateDirectory(Path.Combine(fullProjectPath, "assets"));

                // Create server config
                var serverConfig = new ServerSettings();
                var serverConfigJson = JsonSerializer.Serialize(serverConfig, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(Path.Combine(fullProjectPath, "server_config.json"), serverConfigJson);

                // Create client config
                var clientConfig = new ClientSettings();
                var clientConfigJson = JsonSerializer.Serialize(clientConfig, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(Path.Combine(fullProjectPath, "client_config.json"), clientConfigJson);

                // Create .dme file
                var dmeContent = "// My awesome project\n#include <__DEFINES/std.dm>\n";
                await File.WriteAllTextAsync(Path.Combine(fullProjectPath, $"{projectName}.dme"), dmeContent);

                _logger.LogInformation("Project '{ProjectName}' created successfully.", projectName);

                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to create project '{ProjectName}'", projectName);
                return false;
            }
        }
    }
}
