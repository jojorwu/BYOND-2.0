using System;

namespace Shared.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class SystemAttribute : Attribute
{
    public string? Name { get; set; }
    public string? Group { get; set; }
    public string[]? Dependencies { get; set; }
}
