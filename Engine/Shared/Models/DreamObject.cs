using Shared;
using System.Collections.Generic;

namespace Shared
{
    public class DreamObject
    {
        private readonly object _lock = new();
        public ObjectType ObjectType { get; set; }
        private DreamValue[] _variableValues;

        public DreamObject(ObjectType objectType)
        {
            ObjectType = objectType;
            if (objectType != null)
            {
                _variableValues = new DreamValue[objectType.VariableNames.Count];
                for (int i = 0; i < objectType.FlattenedDefaultValues.Count; i++)
                {
                    _variableValues[i] = DreamValue.FromObject(objectType.FlattenedDefaultValues[i]);
                }
            }
            else
            {
                _variableValues = System.Array.Empty<DreamValue>();
            }
        }

        public virtual DreamValue GetVariable(string name)
        {
            if (ObjectType == null) return DreamValue.Null;
            int index = ObjectType.GetVariableIndex(name);
            if (index == -1) return DreamValue.Null;

            lock (_lock)
            {
                if (index < _variableValues.Length) return _variableValues[index];
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
                    EnsureCapacity(index);
                    _variableValues[index] = value;
                }
            }
        }

        public DreamValue GetVariable(int index)
        {
            lock (_lock)
            {
                if (index >= 0 && index < _variableValues.Length)
                    return _variableValues[index];
            }
            return DreamValue.Null;
        }

        public void SetVariable(int index, DreamValue value)
        {
            lock (_lock)
            {
                EnsureCapacity(index);
                _variableValues[index] = value;
            }
        }

        private void EnsureCapacity(int index)
        {
            if (index >= _variableValues.Length)
            {
                System.Array.Resize(ref _variableValues, index + 1);
            }
        }
    }
}
