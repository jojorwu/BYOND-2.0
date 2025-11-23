using System.Collections.Generic;

namespace Core
{
    public class ObjectType
    {
        public string Name { get; set; }
        public Dictionary<string, object> DefaultProperties { get; set; }

        public ObjectType(string name)
        {
            Name = name;
            DefaultProperties = new Dictionary<string, object>();
        }
    }
}
