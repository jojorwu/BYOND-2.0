using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Core.Api
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
                var safePath = PathSanitizer.Sanitize(_project, filename, Constants.ScriptsRoot);
                return File.Exists(safePath);
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
        }

        public string ReadScriptFile(string filename)
        {
            var safePath = PathSanitizer.Sanitize(_project, filename, Constants.ScriptsRoot);
            return File.ReadAllText(safePath);
        }

        public void WriteScriptFile(string filename, string content)
        {
            var safePath = PathSanitizer.Sanitize(_project, filename, Constants.ScriptsRoot);
            File.WriteAllText(safePath, content);
        }

        public void DeleteScriptFile(string filename)
        {
            var safePath = PathSanitizer.Sanitize(_project, filename, Constants.ScriptsRoot);
            File.Delete(safePath);
        }
    }
}
