using DMCompiler.Compiler;
using Shared;
using Shared.Json;
using System.Collections.Generic;
using System.Linq;

namespace DMCompiler
{
    public class OpenDreamCompilerService : ICompilerService
    {
        public (ICompiledJson?, List<BuildMessage>) Compile(List<string> files)
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

            var compiler = new DMCompiler();
            var (success, _) = compiler.Compile(settings);

            var messages = compiler.CompilerMessages.Select(ConvertCompilerMessage).ToList();

            if (!success)
            {
                return (null, messages);
            }

            var compiledJson = compiler.CreateDreamCompiledJson(new(), null);
            return (compiledJson, messages);
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

            return new BuildMessage(System.IO.Path.GetFileName(file), line, message.Message, level);
        }
    }
}
