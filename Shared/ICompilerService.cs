using System.Collections.Generic;
using DMCompiler.Compiler;

namespace Shared
{
    public interface ICompilerService
    {
        (string?, List<CompilerEmission>) Compile(List<string> files);
    }
}
