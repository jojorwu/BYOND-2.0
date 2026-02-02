using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using Shared.Compiler;

namespace Shared.Interfaces
{
    public interface ICompiledJsonService
    {
        void PopulateState(ICompiledJson compiledJson, IDreamVM dreamVM, IObjectTypeManager typeManager);
    }
}
