
using System.IO;
using System.Text.Json;
using System.Diagnostics;

namespace Core
{
    public class Project
    {
        public string RootPath { get; }
        public ProjectSettings Settings { get; private set; }

        public Project(string rootPath)
        {
            RootPath = rootPath;
            Settings = LoadSettings();
        }

        public string GetFullPath(string relativePath)
        {
            return Path.Combine(RootPath, relativePath);
        }

        public void SaveSettings()
        {
            var path = GetFullPath("project.json");
            var json = JsonSerializer.Serialize(Settings, typeof(ProjectSettings), JsonContext.Default);
            File.WriteAllText(path, json);
        }

        private ProjectSettings LoadSettings()
        {
            var path = GetFullPath("project.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return (ProjectSettings?)JsonSerializer.Deserialize(json, typeof(ProjectSettings), JsonContext.Default) ?? new ProjectSettings();
            }
            return new ProjectSettings();
        }

        public static Project Create(string basePath, string projectName)
        {
            var projectPath = Path.Combine(basePath, projectName);

            if (Directory.Exists(projectPath))
            {
                throw new IOException($"Project directory already exists: {projectPath}");
            }

            // Create project structure
            Directory.CreateDirectory(projectPath);
            Directory.CreateDirectory(Path.Combine(projectPath, "maps"));
            Directory.CreateDirectory(Path.Combine(projectPath, "scripts"));
            Directory.CreateDirectory(Path.Combine(projectPath, "assets"));

            var project = new Project(projectPath);
            project.SaveSettings();

            // Publish the server executable
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish Server/Server.csproj --configuration Release --output \"{projectPath}\" -p:PublishSingleFile=true -p:PublishTrimmed=true",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (var process = Process.Start(startInfo))
            {
                process?.WaitForExit();
            }

            return project;
        }
    }
}
