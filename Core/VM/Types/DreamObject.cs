using Shared;
using System.Collections.Generic;

namespace Core.VM.Types
{
    public class DreamObject
    {
        public string Name { get; set; } = string.Empty;
        public string Desc { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;

        public ObjectType Type { get; }
        public List<DreamValue> Variables { get; }

        public DreamObject(ObjectType type) {
            Type = type;
            Variables = new List<DreamValue>(type.Variables?.Count ?? 0);
            if (type.Variables != null) {
                foreach (var value in type.Variables) {
                    Variables.Add(DreamValue.FromObject(value));
                }
            }
        }

        public virtual DreamValue GetVariable(int id)
        {
            return Variables[id];
        }

        public virtual void SetVariable(int id, DreamValue value)
        {
            Variables[id] = value;
        }
    }
}
