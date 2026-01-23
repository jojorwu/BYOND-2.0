using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Shared
{
    public class ObjectType
    {
        public int Id { get; }
        public string Name { get; set; }
        public string? ParentName { get; set; }

        [JsonIgnore]
        public ObjectType? Parent { get; set; }
        public List<object>? Variables { get; set; }
        public Dictionary<string, int>? VariableNameIds { get; set; }

        public ObjectType(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public bool IsSubtypeOf(ObjectType other)
        {
            var current = this;
            while (current != null)
            {
                if (current == other)
                {
                    return true;
                }
                current = current.Parent;
            }
            return false;
        }
    }
}
