using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Shared;
    public class DreamList : DreamObject
    {
        private const int MaxListSize = 100000000;
        private const int DictionaryThreshold = 8;
        private readonly List<DreamValue> _values;
        public IReadOnlyList<DreamValue> Values => _values;
        private Dictionary<DreamValue, DreamValue>? _associativeValues;
        public Dictionary<DreamValue, DreamValue> AssociativeValues => _associativeValues ??= new();
        private Dictionary<DreamValue, int>? _valueCounts;

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
                if (size >= DictionaryThreshold)
                {
                    _valueCounts = new Dictionary<DreamValue, int> { [DreamValue.Null] = size };
                }
            }
        }

        public void Populate(ReadOnlySpan<DreamValue> initialValues)
        {
            if (initialValues.Length > MaxListSize)
                throw new System.InvalidOperationException("Maximum list size exceeded");

            lock (_lock)
            {
                _values.Clear();
                _associativeValues?.Clear();
                var counts = _valueCounts;
                counts?.Clear();

                if (initialValues.IsEmpty) return;

                if (_values.Capacity < initialValues.Length)
                    _values.Capacity = initialValues.Length;

                foreach (var val in initialValues)
                {
                    _values.Add(val);
                }

                if (initialValues.Length >= DictionaryThreshold)
                {
                    _valueCounts = counts = new Dictionary<DreamValue, int>(initialValues.Length);
                    foreach (var val in initialValues)
                    {
                        if (counts.TryGetValue(val, out int c)) counts[val] = c + 1;
                        else counts[val] = 1;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetValue(DreamValue key, DreamValue value)
        {
            lock (_lock)
            {
                AssociativeValues[key] = value;
                if (!ContainsInternal(key))
                {
                    if (_values.Count >= MaxListSize)
                        throw new System.InvalidOperationException("Maximum list size exceeded");

                    _values.Add(key);
                    AddCount(key);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddValue(DreamValue value)
        {
            lock (_lock)
            {
                if (_values.Count >= MaxListSize)
                    throw new System.InvalidOperationException("Maximum list size exceeded");

                _values.Add(value);
                AddCount(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveValue(DreamValue value)
        {
            lock (_lock)
            {
                if (_values.Remove(value))
                {
                    if (RemoveCount(value))
                    {
                        if (_associativeValues != null) _associativeValues.Remove(value);
                    }
                }
            }
        }

        public void RemoveAll(DreamValue value)
        {
            lock (_lock)
            {
                int writeIndex = 0;
                bool found = false;
                for (int readIndex = 0; readIndex < _values.Count; readIndex++)
                {
                    if (_values[readIndex] == value)
                    {
                        found = true;
                        continue;
                    }

                    if (writeIndex != readIndex)
                    {
                        _values[writeIndex] = _values[readIndex];
                    }
                    writeIndex++;
                }

                if (found)
                {
                    _values.RemoveRange(writeIndex, _values.Count - writeIndex);
                    if (_valueCounts != null) _valueCounts.Remove(value);
                    if (_associativeValues != null) _associativeValues.Remove(value);
                }
            }
        }

        public DreamList Clone()
        {
            lock (_lock)
            {
                var clone = new DreamList(ObjectType);
                if (_values.Count > 0)
                {
                    clone._values.Capacity = _values.Count;
                    clone._values.AddRange(_values);
                }

                if (_valueCounts != null && _valueCounts.Count > 0)
                {
                    clone._valueCounts = new Dictionary<DreamValue, int>(_valueCounts);
                }

                if (_associativeValues != null && _associativeValues.Count > 0)
                {
                    clone._associativeValues = new Dictionary<DreamValue, DreamValue>(_associativeValues);
                }
                return clone;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetValue(int index, DreamValue value)
        {
            lock (_lock)
            {
                if (index >= 0 && index < _values.Count)
                {
                    var old = _values[index];
                    if (old == value) return;

                    _values[index] = value;
                    if (RemoveCount(old))
                    {
                        if (_associativeValues != null) _associativeValues.Remove(old);
                    }
                    AddCount(value);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(DreamValue value)
        {
            lock (_lock)
            {
                return ContainsInternal(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ContainsInternal(DreamValue value)
        {
            var counts = _valueCounts;
            if (counts != null)
                return counts.ContainsKey(value);

            // Linear search for small lists
            var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_values);
            for (int i = 0; i < span.Length; i++)
            {
                // Optimization: avoid full == check if bitwise identical (fast for floats/nulls/refs)
                if (span[i].Equals(value)) return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DreamValue GetValue(DreamValue key)
        {
            lock (_lock)
            {
                if (_associativeValues != null && _associativeValues.TryGetValue(key, out var value))
                {
                    return value;
                }
                return DreamValue.Null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DreamValue GetValue(int index)
        {
            lock (_lock)
            {
                if (index >= 0 && index < _values.Count) return _values[index];
                return DreamValue.Null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool AddCount(DreamValue value)
        {
            if (_valueCounts == null)
            {
                if (_values.Count >= DictionaryThreshold)
                {
                    _valueCounts = new Dictionary<DreamValue, int>(_values.Count);
                    var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_values);
                    for (int i = 0; i < span.Length; i++)
                    {
                        var v = span[i];
                        if (_valueCounts.TryGetValue(v, out int c)) _valueCounts[v] = c + 1;
                        else _valueCounts[v] = 1;
                    }
                }
                return true;
            }

            if (_valueCounts.TryGetValue(value, out int count))
            {
                _valueCounts[value] = count + 1;
                return false;
            }
            _valueCounts[value] = 1;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool RemoveCount(DreamValue value)
        {
            if (_valueCounts == null)
            {
                if (_associativeValues != null)
                {
                    var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_values);
                    for (int i = 0; i < span.Length; i++)
                    {
                        if (span[i].Equals(value)) return false;
                    }
                    return true;
                }
                return true;
            }

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
            return true;
        }
    }
