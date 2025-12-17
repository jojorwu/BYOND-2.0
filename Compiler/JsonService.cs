using Shared;
using System.IO;
using System.Text.Json;
using DMCompiler.Json;

namespace Compiler
{
    public class JsonService : IJsonService
    {
        public IPublicDreamCompiledJson Load(string path)
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<DreamCompiledJson>(json) ?? throw new JsonException("Failed to deserialize compiled json");
        }
    }
}
