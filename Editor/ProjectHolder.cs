using Core;
using System.Collections.Generic;

namespace Editor
{
    public class ProjectHolder : IProject
    {
        private IProject? _project;

        public void SetProject(IProject project)
        {
            _project = project;
        }

        public string RootPath => _project?.RootPath ?? string.Empty;

        public string GetFullPath(string relativePath)
        {
            if (_project == null)
                throw new System.InvalidOperationException("No project is currently loaded.");
            return _project.GetFullPath(relativePath);
        }

        public List<string> GetDmFiles()
        {
            if (_project == null)
                return new List<string>();
            return _project.GetDmFiles();
        }
    }
}
