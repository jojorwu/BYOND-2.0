using Shared;
using System.Collections.Generic;

namespace Shared
{
    public class DreamObject
    {
        public ObjectType ObjectType { get; set; }
        public List<DreamValue> VariableValues { get; } = new();

        public DreamObject(ObjectType objectType)
        {
            ObjectType = objectType;
            if (objectType != null)
            {
                foreach (var defaultValue in objectType.FlattenedDefaultValues)
                {
                    VariableValues.Add(DreamValue.FromObject(defaultValue));
                }
            }
        }

        public virtual DreamValue GetVariable(string name)
        {
            if (ObjectType == null) return DreamValue.Null;
            int index = ObjectType.GetVariableIndex(name);
            if (index != -1 && index < VariableValues.Count) return VariableValues[index];
            return DreamValue.Null;
        }

        public virtual void SetVariable(string name, DreamValue value)
        {
            if (ObjectType == null) return;
            int index = ObjectType.GetVariableIndex(name);
            if (index != -1)
            {
                while (VariableValues.Count <= index) VariableValues.Add(DreamValue.Null);
                VariableValues[index] = value;
            }
        }

        public DreamValue GetVariable(int index)
        {
            if (index >= 0 && index < VariableValues.Count)
                return VariableValues[index];
            return DreamValue.Null;
        }

        public void SetVariable(int index, DreamValue value)
        {
            while (VariableValues.Count <= index) VariableValues.Add(DreamValue.Null);
            VariableValues[index] = value;
        }
    }
}
