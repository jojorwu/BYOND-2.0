using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace Core
{
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(List<ObjectType>))]
    [JsonSerializable(typeof(EngineSettings))]
    [JsonSerializable(typeof(Map))]
    [JsonSerializable(typeof(MapData))]
    [JsonSerializable(typeof(ProjectSettings))]
    internal partial class JsonContext : JsonSerializerContext
    {
    }
}
