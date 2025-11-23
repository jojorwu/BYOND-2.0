
using System.IO;

namespace Core
{
    public class Project
    {
        public string RootPath { get; }

        public Project(string rootPath)
        {
            RootPath = rootPath;
        }

        public string GetFullPath(string relativePath)
        {
            return Path.Combine(RootPath, relativePath);
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

            return new Project(projectPath);
        }
    }
}
