using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shared.Attributes;
using Shared.Enums;
using Shared.Interfaces;

namespace Shared.Models;

public abstract class BaseSystem : ISystem
{
    public virtual string Name => GetType().Name;
    public virtual ExecutionPhase Phase => ExecutionPhase.Simulation;
    public virtual int Priority => 0;
    public virtual bool Enabled => true;
    public virtual string? Group => null;

    private readonly Type[] _readResources;
    private readonly Type[] _writeResources;

    protected BaseSystem()
    {
        var attributes = GetType().GetCustomAttributes<ResourceAttribute>(true);
        _readResources = attributes
            .Where(a => a.Access == ResourceAccess.Read || a.Access == ResourceAccess.ReadWrite)
            .Select(a => a.ResourceType)
            .ToArray();

        _writeResources = attributes
            .Where(a => a.Access == ResourceAccess.Write || a.Access == ResourceAccess.ReadWrite)
            .Select(a => a.ResourceType)
            .ToArray();
    }

    public virtual void Initialize() { }
    public virtual void Shutdown() { }
    public virtual void PreTick() { }
    public abstract void Tick(IEntityCommandBuffer ecb);
    public virtual void PostTick() { }

    public virtual IEnumerable<IJob> CreateJobs() => Array.Empty<IJob>();
    public virtual IEnumerable<string> Dependencies => Array.Empty<string>();

    public virtual IEnumerable<Type> ReadResources => _readResources;
    public virtual IEnumerable<Type> WriteResources => _writeResources;
}
