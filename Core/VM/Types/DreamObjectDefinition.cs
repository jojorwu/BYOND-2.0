using System.Collections.Generic;

namespace Core.VM.Types
{
    public class DreamObjectDefinition
    {
        public string Type;
        public DreamObjectDefinition? Parent;
        public Dictionary<string, DreamValue> Variables = new();

        public DreamObjectDefinition(string type)
        {
            Type = type;
        }

        public bool IsSubtypeOf(DreamObjectDefinition ancestor)
        {
            var current = this;
            while (current != null)
            {
                if (current == ancestor)
                    return true;
                current = current.Parent;
            }

            return false;
        }
    }
}
