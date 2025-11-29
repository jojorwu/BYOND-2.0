using DMCompiler.DM;

namespace DMCompiler.Json;

public sealed class PublicProcDefinitionJson {
    public int OwningTypeId { get; init; }
    public required string Name { get; init; }
    public ProcAttributes Attributes { get; init; }

    public int MaxStackSize { get; init; }
    public List<PublicProcArgumentJson>? Arguments { get; init; }
    public List<PublicLocalVariableJson>? Locals { get; init; }
    public required List<PublicSourceInfoJson> SourceInfo { get; init; }
    public byte[]? Bytecode { get; init; }

    public bool IsVerb { get; init; }
    public VerbSrc? VerbSrc { get; init; }
    public string? VerbName { get; init; }
    public string? VerbCategory { get; init; }
    public string? VerbDesc { get; init; }
    public sbyte Invisibility { get; init; }
}

public struct PublicProcArgumentJson {
    public required string Name { get; init; }
    public DMValueType Type { get; init; }
}

public struct PublicLocalVariableJson {
    public int Offset { get; init; }
    public int? Remove { get; init; }
    public string? Add { get; init; }
}

public struct PublicSourceInfoJson {
    public int Offset { get; init; }
    public int? File { get; set; }
    public int Line { get; init; }
}
