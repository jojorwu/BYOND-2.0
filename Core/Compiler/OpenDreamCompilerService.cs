using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DMCompiler;

namespace Core.Compiler
{
    public class OpenDreamCompilerService : ICompilerService
    {
        private readonly IProject _project;

        public OpenDreamCompilerService(IProject project)
        {
            _project = project;
        }

        public (string?, List<BuildMessage>) Compile(List<string> files)
        {
            if (files == null || files.Count == 0)
            {
                return (null, new List<BuildMessage>());
            }

            var settings = new DMCompilerSettings
            {
                Files = files,
                StoreMessages = true
            };

            var compiler = new DMCompiler.DMCompiler();
            var (success, outputPath) = compiler.Compile(settings);

            var messages = compiler.CompilerMessages.Select(ConvertCompilerMessage).ToList();

            if (!success)
            {
                return (null, messages);
            }

            return (outputPath, messages);
        }

        private BuildMessage ConvertCompilerMessage(DMCompiler.Compiler.CompilerEmission message)
        {
            var level = message.Level switch
            {
                DMCompiler.Compiler.ErrorLevel.Error => BuildMessageLevel.Error,
                DMCompiler.Compiler.ErrorLevel.Warning => BuildMessageLevel.Warning,
                _ => BuildMessageLevel.Info
            };

            var file = message.Location.SourceFile ?? "";
            var line = message.Location.Line ?? 0;

            return new BuildMessage(Path.GetFileName(file), line, message.Message, level);
        }
    }
}
