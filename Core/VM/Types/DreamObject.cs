using System.Collections.Generic;

namespace Core.VM.Types
{
    public class DreamObject
    {
        public DreamObjectDefinition ObjectDefinition;
        private readonly Dictionary<string, DreamValue> _variables = new();

        public DreamObject(DreamObjectDefinition objectDefinition)
        {
            ObjectDefinition = objectDefinition;
        }

        public bool HasVariable(string name)
        {
            return ObjectDefinition.Variables.ContainsKey(name);
        }

        public DreamValue GetVariable(string name)
        {
            if (_variables.TryGetValue(name, out var value))
            {
                return value;
            }

            return ObjectDefinition.Variables.TryGetValue(name, out value) ? value : DreamValue.Null;
        }

        public void SetVariable(string name, DreamValue value)
        {
            _variables[name] = value;
        }
    }
}
