using Shared.Compiler;

namespace Shared;
    public interface IJsonService
    {
        ICompiledJson? Deserialize(string json);
    }
