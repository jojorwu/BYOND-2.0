using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Shared;
using Shared.Compiler;

namespace DMCompiler
{
    public class CompilerService : ICompilerService
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

            var compiler = new global::DMCompiler.DMCompiler();
            var (success, outputPath) = compiler.Compile(settings);

            var messages = compiler.CompilerMessages.Select(ConvertCompilerMessage).ToList();

            if (!success || outputPath == null)
            {
                return (null, messages);
            }

            var json = File.ReadAllText(outputPath);
            var compiledJson = JsonSerializer.Deserialize<CompiledJson>(json, new JsonSerializerOptions() {
                PropertyNameCaseInsensitive = true
            });

            return (compiledJson, messages);
        }

        private BuildMessage ConvertCompilerMessage(Compiler.CompilerEmission message)
        {
            var level = message.Level switch
            {
                Compiler.ErrorLevel.Error => BuildMessageLevel.Error,
                Compiler.ErrorLevel.Warning => BuildMessageLevel.Warning,
                _ => BuildMessageLevel.Info
            };

            var file = message.Location.SourceFile ?? "";
            var line = message.Location.Line ?? 0;

            return new BuildMessage(Path.GetFileName(file), line, message.Message, level);
        }
    }
}
