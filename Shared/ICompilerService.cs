using System.Collections.Generic;
using Shared.Json;

namespace Shared
{
    public interface ICompilerService
    {
        (ICompiledJson?, List<BuildMessage>) Compile(List<string> files);
    }
}
