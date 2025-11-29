using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DMCompiler;

namespace Core
{
    public class OpenDreamCompilerService
    {
        private readonly Project _project;

        public OpenDreamCompilerService(Project project)
        {
            _project = project;
        }

        public (string? OutputPath, List<DMCompiler.Compiler.CompilerEmission> Messages) Compile(List<string> files)
        {
            if (files == null || files.Count == 0)
            {
                return (null, new List<DMCompiler.Compiler.CompilerEmission>());
            }

            var settings = new DMCompilerSettings
            {
                Files = files,
                StoreMessages = true
            };

            var compiler = new DMCompiler.DMCompiler();
            var (success, outputPath) = compiler.Compile(settings);

            if (!success)
            {
                return (null, compiler.CompilerMessages);
            }

            return (outputPath, compiler.CompilerMessages);
        }
    }
}
