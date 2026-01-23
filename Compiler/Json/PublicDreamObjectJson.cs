namespace DMCompiler.Json;

public enum PublicJsonVariableType {
    Resource = 0,
    Type = 1,
    Proc = 2,
    List = 3,
    PositiveInfinity = 4,
    NegativeInfinity = 5,
    AList = 6
}

public sealed class PublicDreamTypeJson {
    public required string Path { get; set; }
    public int? Parent { get; set; }
    public int? InitProc { get; set; }
    public List<List<int>>? Procs { get; set; }
    public HashSet<string>? Verbs { get; set; }
    public List<object>? Variables { get; set; }
    public Dictionary<string, int>? VariableNameIds { get; set; }
    public Dictionary<string, int>? GlobalVariables { get; set; }
    public HashSet<string>? ConstVariables { get; set; }
    public HashSet<string>? TmpVariables { get; set; }
}
