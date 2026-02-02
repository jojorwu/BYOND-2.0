using System.Text.Json;
using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
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
