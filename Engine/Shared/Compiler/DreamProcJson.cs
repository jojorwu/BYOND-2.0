namespace Shared.Compiler;

[Flags]
public enum ProcAttributes : ushort {
    None = 0,
    IsVerb = 1 << 0, // DONT MOVE, used by client
    // 1 << 1 was IsCommand
    // Inferred Verb attributes
    InferredName = 1 << 2,
    InferredCategory = 1 << 3,
    InferredDesc = 1 << 4,
    // End inferred Verb attributes
    Hidden = 1 << 5, // proc is hidden from verb menus
    NeverEnter = 1 << 6, // cannot be directly called by players
    DisableWaitfor = 1 << 7, // Passes for waitfor=FALSE
    Spawn = 1 << 8, // The proc is spawned()
    Background = 1 << 9, // Proc runs in the background
    Stub = 1 << 10, // This proc is just a stub for a var edit and shouldn't be called directly
    NoFilter = 1 << 11, // This proc has unflushed filters and should not be called directly
}

[Flags]
public enum DMValueType : uint {
    Null = 0x01,
    Number = 0x02,
    String = 0x04,
    Resource = 0x08, // a.k.a. "file"
    Type = 0x10, // a.k.a. "path"
    Proc = 0x20, // a.k.a. "verb"
    List = 0x40,
    Mob = 0x80, // a.k.a. "movable"
    Turf = 0x100,
    Obj = 0x200,
    Area = 0x400,
    Client = 0x800,
    Image = 0x1000,
    World = 0x2000,
    Datum = 0x4000,
    Savefile = 0x8000,
    Sound = 0x10000,
    Appearance = 0x20000,

    Anything = 0x3FFFF,
    Unimplemented = 0x80000000 // For when the compiler finds a type it doesn't know about
}

public enum VerbSrc : byte {
    Any = 0,
    None = 1,
    View = 2,
    OView = 3,
    Usr = 4,
    UsrLoc = 5,
    UsrGroup = 6,
    InRange = 7,
    OInRange = 8
}

public sealed class ProcDefinitionJson {
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
}

public struct ProcArgumentJson {
    public required string Name { get; init; }
    public DMValueType Type { get; init; }
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
