using Shared;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Shared
{
    public class DreamObject
    {
        private readonly object _lock = new();
        public ObjectType? ObjectType { get; set; }
        private DreamValue[] _variableValues;

        public DreamObject(ObjectType? objectType)
        {
            ObjectType = objectType;
            if (objectType != null)
            {
                int count = objectType.VariableNames.Count;
                _variableValues = count > 0 ? new DreamValue[count] : System.Array.Empty<DreamValue>();

                int defaultCount = objectType.FlattenedDefaultValues.Count;
                for (int i = 0; i < defaultCount && i < _variableValues.Length; i++)
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
            return index != -1 ? GetVariable(index) : DreamValue.Null;
        }

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
                return GetVariableDirect(index);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DreamValue GetVariableDirect(int index)
        {
            var values = _variableValues;
            if (index >= 0 && index < values.Length)
                return values[index];
            return DreamValue.Null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVariable(int index, DreamValue value)
        {
            lock (_lock)
            {
                SetVariableDirect(index, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVariableDirect(int index, DreamValue value)
        {
            EnsureCapacity(index);
            _variableValues[index] = value;
        }

        private void EnsureCapacity(int index)
        {
            if (index >= _variableValues.Length)
            {
                System.Array.Resize(ref _variableValues, index + 1);
            }
        }

        public override string ToString()
        {
            return ObjectType?.Name ?? "object";
        }
    }
}
