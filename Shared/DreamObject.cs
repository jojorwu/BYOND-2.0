using Shared;
using System.Collections.Generic;

namespace Shared
{
    public class DreamObject
    {
        public ObjectType ObjectType { get; }
        public List<DreamValue> VariableValues { get; } = new();

        public DreamObject(ObjectType objectType)
        {
            ObjectType = objectType;
            foreach (var defaultValue in objectType.FlattenedDefaultValues)
            {
                VariableValues.Add(DreamValue.FromObject(defaultValue));
            }
        }

        public virtual DreamValue GetVariable(string name)
        {
            int index = ObjectType.VariableNames.IndexOf(name);
            if (index != -1) return VariableValues[index];
            return DreamValue.Null;
        }

        public virtual void SetVariable(string name, DreamValue value)
        {
            int index = ObjectType.VariableNames.IndexOf(name);
            if (index != -1) VariableValues[index] = value;
        }

        public DreamValue GetVariable(int index)
        {
            return VariableValues[index];
        }

        public void SetVariable(int index, DreamValue value)
        {
            VariableValues[index] = value;
        }
    }
}
