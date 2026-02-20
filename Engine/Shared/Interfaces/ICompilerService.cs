using System.Collections.Generic;
using Shared.Compiler;

namespace Shared;
    public interface ICompilerService
    {
        (ICompiledJson?, List<BuildMessage>) Compile(List<string> files);
    }
