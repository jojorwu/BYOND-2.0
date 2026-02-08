using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Shared
{
    public class DreamList : DreamObject
    {
        private const int MaxListSize = 1000000;
        private readonly List<DreamValue> _values;
        public IReadOnlyList<DreamValue> Values => _values;
        public Dictionary<DreamValue, DreamValue> AssociativeValues { get; } = new();
        private readonly Dictionary<DreamValue, int> _valueCounts = new();

        public DreamList(ObjectType? listType) : base(listType)
        {
            _values = new List<DreamValue>();
        }

        public DreamList(ObjectType? listType, int size) : base(listType)
        {
            if (size < 0 || size > MaxListSize)
                throw new System.ArgumentException($"Invalid list size: {size}", nameof(size));

            _values = new List<DreamValue>(size);
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
            if (!_valueCounts.ContainsKey(key))
            {
                if (_values.Count >= MaxListSize)
                    throw new System.InvalidOperationException("Maximum list size exceeded");

                _values.Add(key);
                _valueCounts[key] = 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddValue(DreamValue value)
        {
            if (_values.Count >= MaxListSize)
                throw new System.InvalidOperationException("Maximum list size exceeded");

            _values.Add(value);
            AddCount(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveValue(DreamValue value)
        {
            if (_values.Remove(value))
            {
                if (RemoveCount(value))
                {
                    AssociativeValues.Remove(value);
                }
            }
        }

        public void RemoveAll(DreamValue value)
        {
            while (Contains(value))
            {
                RemoveValue(value);
            }
        }

        public DreamList Clone()
        {
            var clone = new DreamList(ObjectType, _values.Count);
            for (int i = 0; i < _values.Count; i++)
            {
                clone.SetValue(i, _values[i]);
            }
            foreach (var kvp in AssociativeValues)
            {
                clone.AssociativeValues[kvp.Key] = kvp.Value;
            }
            return clone;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetValue(int index, DreamValue value)
        {
            if (index >= 0 && index < _values.Count)
            {
                var old = _values[index];
                _values[index] = value;
                if (RemoveCount(old))
                {
                    AssociativeValues.Remove(old);
                }
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

        /// <summary>
        /// Removes a count from the value count cache.
        /// </summary>
        /// <param name="value">The value to decrement count for.</param>
        /// <returns>True if the value was completely removed from the cache (count reached 0).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool RemoveCount(DreamValue value)
        {
            if (_valueCounts.TryGetValue(value, out int count))
            {
                if (count <= 1)
                {
                    _valueCounts.Remove(value);
                    return true;
                }
                else
                {
                    _valueCounts[value] = count - 1;
                    return false;
                }
            }
            return false;
        }
    }
}
