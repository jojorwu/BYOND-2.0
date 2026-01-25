using DMCompiler.DM;
using Shared.Json;
using System.Collections.Generic;
using System.Linq;

namespace DMCompiler.Json;

public sealed class ProcDefinitionJson : ICompiledProcJson {
    public int OwningTypeId { get; init; }
    public required string Name { get; init; }
    public ProcAttributes Attributes { get; init; }

    public int MaxStackSize { get; init; }
    public List<ProcArgumentJson>? Arguments { get; init; }
    public List<LocalVariableJson>? Locals { get; init; }
    public required List<SourceInfoJson> SourceInfo { get; init; }
    public byte[]? Bytecode { get; init; }

    public bool IsVerb { get; init; }
    public VerbSrc? VerbSrc { get; init; }
    public string? VerbName { get; init; }
    public string? VerbCategory { get; init; }
    public string? VerbDesc { get; init; }
    public sbyte Invisibility { get; init; }
    IReadOnlyList<byte> ICompiledProcJson.Bytecode => Bytecode;
    IReadOnlyList<ICompiledArgumentJson> ICompiledProcJson.Arguments => Arguments?.Cast<ICompiledArgumentJson>().ToList();
    int ICompiledProcJson.Locals => Locals?.Count ?? 0;
    Shared.ProcAttributes ICompiledProcJson.Attributes => (Shared.ProcAttributes)Attributes;
}

public struct ProcArgumentJson : ICompiledArgumentJson {
    public required string Name { get; init; }
    public DMValueType Type { get; init; }
    Shared.DMValueType ICompiledArgumentJson.Type => (Shared.DMValueType)Type;
}

public struct LocalVariableJson {
    public int Offset { get; init; }
    public int? Remove { get; init; }
    public string? Add { get; init; }
}

public struct SourceInfoJson {
    public int Offset { get; init; }
    public int? File { get; set; }
    public int Line { get; init; }
}

public class LineComparer : IEqualityComparer<SourceInfoJson> {
    public bool Equals(SourceInfoJson? x, SourceInfoJson? y) {
        return x?.Line == y?.Line;
    }

    public bool Equals(SourceInfoJson x, SourceInfoJson y) {
        return x.Line == y.Line;
    }

    public int GetHashCode(SourceInfoJson obj) {
        return obj.Line.GetHashCode();
    }
}
