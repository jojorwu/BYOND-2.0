using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Shared
{
    public class DreamObject
    {
        private readonly object _lock = new();
        public ObjectType ObjectType { get; set; }
        private DreamValue[] _variableValues;

        public DreamObject(ObjectType? objectType)
        {
            ObjectType = objectType!;
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
                _variableValues = [];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual DreamValue GetVariable(string name)
        {
            if (ObjectType == null) return DreamValue.Null;
            int index = ObjectType.GetVariableIndex(name);
            return index != -1 ? GetVariable(index) : DreamValue.Null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void SetVariable(string name, DreamValue value)
        {
            if (ObjectType == null) return;
            int index = ObjectType.GetVariableIndex(name);
            if (index != -1)
            {
                SetVariable(index, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DreamValue GetVariable(int index)
        {
            lock (_lock)
            {
                if (index >= 0 && index < _variableValues.Length)
                    return _variableValues[index];
            }
            return DreamValue.Null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
