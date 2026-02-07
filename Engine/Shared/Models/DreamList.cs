using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Shared;

namespace Shared.Models
{
    public class DreamList : DreamObject
    {
        private readonly List<DreamValue> _values = new();
        public IReadOnlyList<DreamValue> Values => _values;
        public Dictionary<DreamValue, DreamValue> AssociativeValues { get; } = new();
        private readonly Dictionary<DreamValue, int> _valueCounts = new();

        public DreamList(ObjectType? listType) : base(listType)
        {
        }

        public DreamList(ObjectType? listType, int size) : base(listType)
        {
            if (size > 0)
            {
                for (int i = 0; i < size; i++)
                {
                    _values.Add(DreamValue.Null);
                }
                _valueCounts[DreamValue.Null] = size;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetValue(DreamValue key, DreamValue value)
        {
            AssociativeValues[key] = value;
            if (AddCount(key))
            {
                _values.Add(key);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddValue(DreamValue value)
        {
            _values.Add(value);
            AddCount(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveValue(DreamValue value)
        {
            if (_values.Remove(value))
            {
                RemoveCount(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetValue(int index, DreamValue value)
        {
            if (index >= 0 && index < _values.Count)
            {
                var old = _values[index];
                _values[index] = value;
                RemoveCount(old);
                AddCount(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(DreamValue value)
        {
            return _valueCounts.ContainsKey(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DreamValue GetValue(DreamValue key)
        {
            if (AssociativeValues.TryGetValue(key, out var value))
            {
                return value;
            }
            return DreamValue.Null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool AddCount(DreamValue value)
        {
            if (_valueCounts.TryGetValue(value, out int count))
            {
                _valueCounts[value] = count + 1;
                return false;
            }
            _valueCounts[value] = 1;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveCount(DreamValue value)
        {
            if (_valueCounts.TryGetValue(value, out int count))
            {
                if (count <= 1)
                {
                    _valueCounts.Remove(value);
                }
                else
                {
                    _valueCounts[value] = count - 1;
                }
            }
        }
    }
}
