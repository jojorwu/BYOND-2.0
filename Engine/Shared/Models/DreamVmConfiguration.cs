namespace Shared;

public record DreamVmConfiguration
{
    public int MaxInstructions { get; set; } = 1000000000;
}
