using System;

namespace Shared.Config;

[Flags]
public enum CVarFlags
{
    None = 0,
    Archive = 1 << 0,  // Saved to disk
    Server = 1 << 1,   // Server-side setting
    Client = 1 << 2,   // Client-side setting
    Cheat = 1 << 3,    // Requires cheats enabled
}

public class CVarAttribute : Attribute
{
    public string Name { get; }
    public CVarFlags Flags { get; }
    public string Description { get; }
    public string Category { get; }

    public CVarAttribute(string name, CVarFlags flags = CVarFlags.None, string description = "", string category = "General")
    {
        Name = name;
        Flags = flags;
        Description = description;
        Category = category;
    }
}
