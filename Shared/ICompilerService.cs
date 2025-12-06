using System.Collections.Generic;

namespace Shared
{
    public interface ICompilerService
    {
        (string?, List<BuildMessage>) Compile(List<string> files);
    }
}
