using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Shared
{
    public class ObjectType
    {
        public string Name { get; set; }
        public string? ParentName { get; set; }

        [JsonIgnore]
        public ObjectType? Parent { get; set; }
        public Dictionary<string, object?> DefaultProperties { get; set; }

        public ObjectType(string name)
        {
            Name = name;
            DefaultProperties = new Dictionary<string, object?>();
        }
    }
}
