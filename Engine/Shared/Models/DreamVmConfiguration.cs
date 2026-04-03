namespace Shared;

public record DreamVmConfiguration
{
    public int MaxInstructions { get; set; } = 1000000000;
    public int MaxObjectCount { get; set; } = 1000000;
}
