using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shared.Attributes;
using Shared.Interfaces;

namespace Shared.Models
{
    public abstract class BaseSystem : ISystem
    {
        private readonly SystemAttribute? _systemAttr;
        private readonly List<ResourceAttribute> _resourceAttrs;

        protected BaseSystem()
        {
            var type = GetType();
            _systemAttr = type.GetCustomAttribute<SystemAttribute>();
            _resourceAttrs = type.GetCustomAttributes<ResourceAttribute>().ToList();
        }

        public virtual string Name => _systemAttr?.Name ?? GetType().Name;

        public virtual int Priority => _systemAttr?.Priority ?? 0;

        public virtual IEnumerable<string> Dependencies => _systemAttr?.Dependencies ?? Array.Empty<string>();

        public virtual string? Group => _systemAttr?.Group;

        public virtual bool Enabled => true;

        public virtual IEnumerable<Type> ReadResources => _resourceAttrs
            .Where(a => a.Access == ResourceAccess.Read)
            .Select(a => a.ResourceType);

        public virtual IEnumerable<Type> WriteResources => _resourceAttrs
            .Where(a => a.Access == ResourceAccess.Write)
            .Select(a => a.ResourceType);

        public virtual void Initialize() { }

        public virtual void PreTick() { }

        public abstract void Tick(IEntityCommandBuffer ecb);

        public virtual void PostTick() { }

        public virtual IEnumerable<IJob> CreateJobs() => Array.Empty<IJob>();
    }
}
