using System.Collections.Generic;
using Shared.Json;

namespace Shared
{
    public interface IDmmParserService
    {
        (IMapData?, ICompiledJson?) ParseDmm(List<string> dmFiles, string dmmFilePath);
    }
}
