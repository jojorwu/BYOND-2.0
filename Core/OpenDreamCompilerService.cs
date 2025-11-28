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

        public (DMCompiler.DMCompiler? compiler, List<DMCompiler.Compiler.CompilerEmission> Messages) Compile()
        {
            var dmFiles = _project.GetDmFiles();
            if (dmFiles == null || dmFiles.Count == 0)
            {
                return (null, new List<DMCompiler.Compiler.CompilerEmission>());
            }

            var settings = new DMCompilerSettings
            {
                Files = dmFiles,
                StoreMessages = true
            };

            var compiler = new DMCompiler.DMCompiler();
            compiler.Compile(settings);
            return (compiler, compiler.CompilerMessages);
        }
    }
}
