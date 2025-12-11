namespace DMCompiler.Json;

using Shared;

public sealed class PublicDreamCompiledJson : IPublicDreamCompiledJson {
    public required List<string> Strings { get; set; }
    public required PublicDreamTypeJson[] Types { get; set; }
    public required PublicProcDefinitionJson[] Procs { get; set; }
    public GlobalListJson? Globals { get; set; }
}
