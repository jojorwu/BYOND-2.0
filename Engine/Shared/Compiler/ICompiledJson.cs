namespace Shared.Compiler;

/// <summary>
/// A marker interface for the compiled JSON data structure.
/// This allows Core to depend on an abstraction rather than a concrete type from the Compiler.
/// </summary>
public interface ICompiledJson {
}

/// <summary>
/// The root object of the compiled JSON file.
/// This is the data transfer object (DTO) that decouples Core from the Compiler.
/// </summary>
public sealed class CompiledJson : ICompiledJson {
    public required List<string> Strings { get; set; }
    public required DreamTypeJson[] Types { get; set; }
    public required ProcDefinitionJson[] Procs { get; set; }
    public GlobalListJson? Globals { get; set; }
}
