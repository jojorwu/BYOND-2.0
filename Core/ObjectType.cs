using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Core
{
    public class ObjectType
    {
        public string Name { get; set; }
        public string? ParentName { get; set; }

        [JsonIgnore]
        public ObjectType? Parent { get; set; }
        public Dictionary<string, object?> DefaultProperties { get; set; }

        public ObjectType(string name, ObjectType? parent = null)
        {
            Name = name;
            Parent = parent;
            ParentName = parent?.Name;
            DefaultProperties = new Dictionary<string, object?>();
        }

        public bool IsSubtypeOf(ObjectType other)
        {
            var current = this;
            while (current != null)
            {
                if (current == other)
                    return true;
                current = current.Parent;
            }
            return false;
        }
    }
}
