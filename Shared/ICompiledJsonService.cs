using Shared.Compiler;
using System.Collections.Generic;

namespace Shared
{
    public interface ICompiledJsonService
    {
        void LoadStrings(ICompiledJson compiledJson, List<string> strings);
        void LoadProcs(ICompiledJson compiledJson, Dictionary<string, IDreamProc> procs);
        void LoadTypes(ICompiledJson compiledJson, IObjectTypeManager typeManager);
    }
}
