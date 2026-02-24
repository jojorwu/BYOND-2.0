using System;

namespace Shared.Attributes;

public enum ResourceAccess
{
    Read,
    Write,
    ReadWrite
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ResourceAttribute : Attribute
{
    public Type ResourceType { get; }
    public ResourceAccess Access { get; }

    public ResourceAttribute(Type resourceType, ResourceAccess access = ResourceAccess.ReadWrite)
    {
        ResourceType = resourceType;
        Access = access;
    }
}
