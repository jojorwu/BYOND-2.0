using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;
using Shared.Compiler;

namespace Shared.Interfaces
{
    public interface ICompilerService
    {
        (ICompiledJson?, List<BuildMessage>) Compile(List<string> files);
    }
}
