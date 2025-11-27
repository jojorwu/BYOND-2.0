using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Core;

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
        private readonly OpenDreamCompilerService _compilerService;
        private readonly Project _project;

        public List<BuildMessage> Messages { get; } = new();

        public BuildService(Project project)
        {
            _compilerService = new OpenDreamCompilerService();
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
                Messages.Add(ParseCompilerMessage(message));
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

        private BuildMessage ParseCompilerMessage(string message)
        {
            var match = Regex.Match(message, @"(.+?)\((\d+)\): (.+?): (.+)");
            if (match.Success)
            {
                var file = match.Groups[1].Value;
                var line = int.Parse(match.Groups[2].Value);
                var levelStr = match.Groups[3].Value.ToLower();
                var text = match.Groups[4].Value;

                var level = levelStr switch
                {
                    "error" => BuildMessageLevel.Error,
                    "warning" => BuildMessageLevel.Warning,
                    _ => BuildMessageLevel.Info
                };

                return new BuildMessage(Path.GetFileName(file), line, text, level);
            }
            return new BuildMessage("", 0, message, BuildMessageLevel.Info);
        }
    }
}
