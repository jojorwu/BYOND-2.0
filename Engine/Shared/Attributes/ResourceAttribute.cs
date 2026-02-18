using System;

namespace Shared.Attributes
{
    public enum ResourceAccess
    {
        Read,
        Write
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class ResourceAttribute : Attribute
    {
        public Type ResourceType { get; }
        public ResourceAccess Access { get; }

        public ResourceAttribute(Type resourceType, ResourceAccess access = ResourceAccess.Read)
        {
            ResourceType = resourceType;
            Access = access;
        }
    }
}
