using Shared;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DMCompiler.Compiler;

namespace Editor
{
    public record BuildMessage(string File, int Line, string Text, BuildMessageLevel Level);

    public enum BuildMessageLevel
    {
        Info,
        Warning,
        Error
    }

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

            foreach (var message in compilerMessages)
            {
                if (message.Level == ErrorLevel.Disabled)
                    continue;
                Messages.Add(ConvertCompilerMessage(message));
            }

            if (jsonPath != null)
            {
                Messages.Add(new BuildMessage("", 0, $"Compilation successful. Output: {jsonPath}", BuildMessageLevel.Info));
                File.Delete(jsonPath); // Clean up
            }
            else
            {
                Messages.Add(new BuildMessage("", 0, "Compilation failed.", BuildMessageLevel.Error));
            }
        }

        private BuildMessage ConvertCompilerMessage(CompilerEmission message)
        {
            var level = message.Level switch
            {
                ErrorLevel.Error => BuildMessageLevel.Error,
                ErrorLevel.Warning => BuildMessageLevel.Warning,
                _ => BuildMessageLevel.Info
            };

            var file = message.Location.SourceFile ?? "";
            var line = message.Location.Line ?? 0;

            return new BuildMessage(Path.GetFileName(file), line, message.Message, level);
        }
    }
}
