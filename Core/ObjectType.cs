using System.Collections.Generic;
using Newtonsoft.Json;

namespace Core
{
    public class ObjectType
    {
        public string Name { get; set; }
        public Dictionary<string, object> DefaultProperties { get; set; }

        [JsonIgnore]
        public ObjectType? Parent { get; set; }

        public string? ParentName
        {
            get
            {
                var parts = Name.Split('/');
                if (parts.Length > 1)
                {
                    return string.Join('/', parts[..^1]);
                }
                return null;
            }
        }

        public ObjectType(string name)
        {
            Name = name;
            DefaultProperties = new Dictionary<string, object>();
        }
    }
}
