using Shared.Compiler;

namespace Shared
{
    public interface ICompiledJsonService
    {
        void PopulateState(ICompiledJson compiledJson, IDreamVM dreamVM, IObjectTypeManager typeManager);
    }
}
