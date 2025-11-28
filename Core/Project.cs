using System.IO;
using System.Text.Json;
using System.Linq;

namespace Core
{
    public class Project
    {
        public string RootPath { get; }

        public Project(string rootPath)
        {
            RootPath = rootPath;
        }

        public static Project Create(string path)
        {
            Directory.CreateDirectory(path);
            Directory.CreateDirectory(Path.Combine(path, "maps"));
            Directory.CreateDirectory(Path.Combine(path, "scripts"));
            Directory.CreateDirectory(Path.Combine(path, "assets"));

            var projectSettings = new { MainMap = "maps/default.json" };
            File.WriteAllText(Path.Combine(path, "project.json"), JsonSerializer.Serialize(projectSettings));

            var serverSettings = new ServerSettings();
            File.WriteAllText(Path.Combine(path, "server_config.json"), JsonSerializer.Serialize(serverSettings));

            return new Project(path);
        }

        public string GetFullPath(string relativePath)
        {
            return System.IO.Path.Combine(RootPath, relativePath);
        }

        public List<string> GetDmFiles()
        {
            var scriptsPath = GetFullPath("scripts");
            if (!Directory.Exists(scriptsPath))
            {
                return new List<string>();
            }

            return Directory.GetFiles(scriptsPath, "*.dm", SearchOption.AllDirectories).ToList();
        }
    }
}
