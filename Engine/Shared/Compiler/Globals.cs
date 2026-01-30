namespace Shared.Compiler;

public sealed class GlobalListJson {
    public int GlobalCount { get; set; }
    public required List<string> Names { get; set; }
    public required Dictionary<int, object> Globals { get; set; }
}
