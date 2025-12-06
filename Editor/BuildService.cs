using Shared;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Editor
{
    public class BuildService
    {
        private readonly ICompilerService _compilerService;
        private readonly IProject _project;

        public List<BuildMessage> Messages { get; } = new();

        public BuildService(IProject project, ICompilerService compilerService)
        {
            _compilerService = compilerService;
            _project = project;
        }

        public void CompileProject()
        {
            Messages.Clear();
            var dmFiles = Directory.GetFiles(_project.RootPath, "*.dm", SearchOption.AllDirectories).ToList();
            if (!dmFiles.Any())
            {
                Messages.Add(new BuildMessage("", 0, "No DM files found in the project.", BuildMessageLevel.Info));
                return;
            }

            var (jsonPath, compilerMessages) = _compilerService.Compile(dmFiles);

            Messages.AddRange(compilerMessages);

            if (jsonPath != null)
            {
                Messages.Add(new BuildMessage("", 0, $"Compilation successful. Output: {jsonPath}", BuildMessageLevel.Info));
                File.Delete(jsonPath); // Clean up
            }
            else
            {
                if (!compilerMessages.Any(m => m.Level == BuildMessageLevel.Error))
                {
                    Messages.Add(new BuildMessage("", 0, "Compilation failed.", BuildMessageLevel.Error));
                }
            }
        }
    }
}
