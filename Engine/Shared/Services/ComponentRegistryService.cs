using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Shared.Interfaces;

namespace Shared.Services;

/// <summary>
/// Managed wrapper for the static ComponentIdRegistry to integrate it into the service lifecycle.
/// </summary>
public class ComponentRegistryService : EngineService, IFreezable
{
    public override int Priority => 100; // High priority for early freezing

    public void Freeze()
    {
        ComponentIdRegistry.Freeze();
    }
}
