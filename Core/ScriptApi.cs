using Shared;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Core
{
    public class ScriptApi : IScriptApi
    {
        private readonly IProject _project;

        public ScriptApi(IProject project)
        {
            _project = project;
        }

        public List<string> ListScriptFiles()
        {
            var rootPath = Path.GetFullPath(Constants.ScriptsRoot);
            if (!Directory.Exists(rootPath))
                return new List<string>();

            return Directory.GetFiles(rootPath, "*.lua", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(rootPath, path))
                .ToList();
        }

        public bool ScriptFileExists(string filename)
        {
            try
            {
                var safePath = SanitizePath(filename, Constants.ScriptsRoot);
                return File.Exists(safePath);
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
        }

        public string ReadScriptFile(string filename)
        {
            var safePath = SanitizePath(filename, Constants.ScriptsRoot);
            return File.ReadAllText(safePath);
        }

        public void WriteScriptFile(string filename, string content)
        {
            var safePath = SanitizePath(filename, Constants.ScriptsRoot);
            File.WriteAllText(safePath, content);
        }

        public void DeleteScriptFile(string filename)
        {
            var safePath = SanitizePath(filename, Constants.ScriptsRoot);
            File.Delete(safePath);
        }

        private string SanitizePath(string userProvidedPath, string expectedRootFolder)
        {
            // Get the full path of the project's root for the given type (e.g., /tmp/proj/scripts)
            var fullRootPath = Path.GetFullPath(_project.GetFullPath(expectedRootFolder));

            // Get the full path of the user-provided file relative to the project root
            var fullUserPath = Path.GetFullPath(_project.GetFullPath(userProvidedPath));


            if (!fullUserPath.StartsWith(fullRootPath))
            {
                throw new System.Security.SecurityException("Access to path is denied.");
            }
            return fullUserPath;
        }
    }
}
