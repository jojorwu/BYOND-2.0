using System;

namespace Shared.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class EngineServiceAttribute : Attribute
{
    public Type[] Interfaces { get; }
    public bool IsCritical { get; set; } = true;
    public int Priority { get; set; } = 0;

    public EngineServiceAttribute(params Type[] interfaces)
    {
        Interfaces = interfaces;
    }
}
