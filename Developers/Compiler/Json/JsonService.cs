using System.Text.Json;
using Shared;
using Shared.Compiler;

namespace DMCompiler.Json
{
    public class JsonService : IJsonService
    {
        public ICompiledJson? Deserialize(string json)
        {
            return JsonSerializer.Deserialize<CompiledJson>(json, new JsonSerializerOptions() {
                PropertyNameCaseInsensitive = true
            });
        }
    }
}
