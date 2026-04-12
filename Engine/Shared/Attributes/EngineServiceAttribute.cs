using System;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class EngineServiceAttribute : Attribute
{
    public Type[] Interfaces { get; }
    public bool IsCritical { get; set; } = true;
    public int Priority { get; set; } = 0;
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Singleton;
    public bool AutoRegisterInterfaces { get; set; } = true;

    public EngineServiceAttribute(params Type[] interfaces)
    {
        Interfaces = interfaces;
    }
}
