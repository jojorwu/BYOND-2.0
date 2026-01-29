using Shared;
using System.Collections.Generic;

namespace Shared
{
    public class DreamObject
    {
        private readonly object _lock = new();
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
            if (index == -1) return DreamValue.Null;

            lock (_lock)
            {
                if (index < VariableValues.Count) return VariableValues[index];
            }
            return DreamValue.Null;
        }

        public virtual void SetVariable(string name, DreamValue value)
        {
            if (ObjectType == null) return;
            int index = ObjectType.GetVariableIndex(name);
            if (index != -1)
            {
                lock (_lock)
                {
                    while (VariableValues.Count <= index) VariableValues.Add(DreamValue.Null);
                    VariableValues[index] = value;
                }
            }
        }

        public DreamValue GetVariable(int index)
        {
            lock (_lock)
            {
                if (index >= 0 && index < VariableValues.Count)
                    return VariableValues[index];
            }
            return DreamValue.Null;
        }

        public void SetVariable(int index, DreamValue value)
        {
            lock (_lock)
            {
                while (VariableValues.Count <= index) VariableValues.Add(DreamValue.Null);
                VariableValues[index] = value;
            }
        }
    }
}
