using System;

namespace Shared.Attributes;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class ResourceAttribute : Attribute
{
    public bool ReadOnly { get; set; } = true;
}
