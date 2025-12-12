using Shared;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Core
{
    public class ScriptApi : IScriptApi
    {
        private readonly IProject _project;
        private readonly string _scriptsBasePath;

        public ScriptApi(IProject project)
        {
            _project = project;
            _scriptsBasePath = Path.Combine(_project.RootPath, Constants.ScriptsRoot);
            if (!Directory.Exists(_scriptsBasePath))
            {
                Directory.CreateDirectory(_scriptsBasePath);
            }
        }

        public List<string> ListScriptFiles()
        {
            return Directory.GetFiles(_scriptsBasePath, "*.*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(_scriptsBasePath, path))
                .ToList();
        }

        public bool ScriptFileExists(string filename)
        {
            try
            {
                var safePath = PathSanitizer.Sanitize(_scriptsBasePath, filename);
                return File.Exists(safePath);
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
        }

        public string ReadScriptFile(string filename)
        {
            var safePath = PathSanitizer.Sanitize(_scriptsBasePath, filename);
            return File.ReadAllText(safePath);
        }

        public void WriteScriptFile(string filename, string content)
        {
            var safePath = PathSanitizer.Sanitize(_scriptsBasePath, filename);
            File.WriteAllText(safePath, content);
        }

        public void DeleteScriptFile(string filename)
        {
            var safePath = PathSanitizer.Sanitize(_scriptsBasePath, filename);
            File.Delete(safePath);
        }
    }
}
