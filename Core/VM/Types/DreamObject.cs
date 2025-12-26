using Shared;
using System.Collections.Generic;

namespace Core.VM.Types
{
    public class DreamObject
    {
        public string Name { get; set; } = string.Empty;
        public string Desc { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;

        public Dictionary<string, DreamValue> Variables { get; } = new();

        public virtual DreamValue GetVariable(string name)
        {
            return Variables.TryGetValue(name, out var value) ? value : DreamValue.Null;
        }

        public virtual void SetVariable(string name, DreamValue value)
        {
            Variables[name] = value;
        }
    }
}
