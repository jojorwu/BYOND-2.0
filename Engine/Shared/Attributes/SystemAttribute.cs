using System;

namespace Shared.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class SystemAttribute : Attribute
    {
        public string? Name { get; set; }
        public string? Group { get; set; }
        public string[]? Dependencies { get; set; }
        public int Priority { get; set; } = 0;

        public SystemAttribute() { }

        public SystemAttribute(string name)
        {
            Name = name;
        }
    }
}
