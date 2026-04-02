using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Shared;
    public class DreamList : DreamObject
    {
        private const int MaxListSize = 100000000;
        private const int DictionaryThreshold = 8;
        private readonly List<DreamValue> _values;
        public IReadOnlyList<DreamValue> Values => _values;
        private ConcurrentDictionary<DreamValue, DreamValue>? _associativeValues;
        private DreamValue[]? _linearKeys;
        private DreamValue[]? _linearValues;
        private int _linearCount;
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

            _lock.EnterWriteLock();
            try
            {
                _values.Clear();
                _associativeValues?.Clear();
                var counts = _valueCounts;
                counts?.Clear();

                if (initialValues.IsEmpty) return;

                System.Runtime.InteropServices.CollectionsMarshal.SetCount(_values, initialValues.Length);
                initialValues.CopyTo(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_values));

                if (initialValues.Length >= DictionaryThreshold)
                {
                    _valueCounts = counts = new Dictionary<DreamValue, int>(initialValues.Length);
                    // Use Span-based enumeration for faster population of counts
                    var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_values);
                    for (int i = 0; i < span.Length; i++)
                    {
                        ref var val = ref span[i];
                        if (counts.TryGetValue(val, out int c)) counts[val] = c + 1;
                        else counts[val] = 1;
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetValue(DreamValue key, DreamValue value)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_associativeValues != null)
                {
                    _associativeValues[key] = value;
                }
                else
                {
                    // Linear search/update
                    int idx = -1;
                    if (_linearKeys != null)
                    {
                        for (int i = 0; i < _linearCount; i++)
                        {
                            if (_linearKeys[i].Equals(key)) { idx = i; break; }
                        }
                    }

                    if (idx != -1)
                    {
                        _linearValues![idx] = value;
                    }
                    else
                    {
                        if (_linearCount < DictionaryThreshold)
                        {
                            _linearKeys ??= new DreamValue[DictionaryThreshold];
                            _linearValues ??= new DreamValue[DictionaryThreshold];
                            _linearKeys[_linearCount] = key;
                            _linearValues[_linearCount] = value;
                            _linearCount++;
                        }
                        else
                        {
                            // Promote to dictionary
                            _associativeValues = new ConcurrentDictionary<DreamValue, DreamValue>();
                            for (int i = 0; i < _linearCount; i++) _associativeValues[_linearKeys![i]] = _linearValues![i];
                            _associativeValues[key] = value;
                            _linearKeys = null;
                            _linearValues = null;
                        }
                    }
                }

                if (!ContainsInternal(key))
                {
                    if (_values.Count >= MaxListSize)
                        throw new System.InvalidOperationException("Maximum list size exceeded");

                    _values.Add(key);
                    AddCount(key);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddValue(DreamValue value)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_values.Count >= MaxListSize)
                    throw new System.InvalidOperationException("Maximum list size exceeded");

                _values.Add(value);
                AddCount(value);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveValue(DreamValue value)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_values.Remove(value))
                {
                    if (RemoveCount(value))
                    {
                        if (_associativeValues != null) _associativeValues.TryRemove(value, out _);
                        else if (_linearKeys != null)
                        {
                            for (int i = 0; i < _linearCount; i++)
                            {
                                if (_linearKeys[i].Equals(value))
                                {
                                    _linearKeys[i] = _linearKeys[_linearCount - 1];
                                    _linearValues![i] = _linearValues![_linearCount - 1];
                                    _linearKeys[_linearCount - 1] = default;
                                    _linearValues![_linearCount - 1] = default;
                                    _linearCount--;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void RemoveAll(DreamValue value)
        {
            _lock.EnterWriteLock();
            try
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
                    if (_associativeValues != null) _associativeValues.TryRemove(value, out _);
                    else if (_linearKeys != null)
                    {
                        for (int i = 0; i < _linearCount; i++)
                        {
                            if (_linearKeys[i].Equals(value))
                            {
                                _linearKeys[i] = _linearKeys[_linearCount - 1];
                                _linearValues![i] = _linearValues![_linearCount - 1];
                                _linearKeys[_linearCount - 1] = default;
                                _linearValues![_linearCount - 1] = default;
                                _linearCount--;
                                break;
                            }
                        }
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public DreamList Clone()
        {
            _lock.EnterReadLock();
            try
            {
                int count = _values.Count;
                var clone = new DreamList(ObjectType);
                if (count > 0)
                {
                    System.Runtime.InteropServices.CollectionsMarshal.SetCount(clone._values, count);
                    System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_values).CopyTo(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(clone._values));
                }

                if (_valueCounts != null && _valueCounts.Count > 0)
                {
                    clone._valueCounts = new Dictionary<DreamValue, int>(_valueCounts);
                }

                if (_associativeValues != null && _associativeValues.Count > 0)
                {
                    clone._associativeValues = new ConcurrentDictionary<DreamValue, DreamValue>(_associativeValues);
                }
                return clone;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetValue(int index, DreamValue value)
        {
            _lock.EnterWriteLock();
            try
            {
                if (index >= 0 && index < _values.Count)
                {
                    var old = _values[index];
                    if (old == value) return;

                    _values[index] = value;
                    if (RemoveCount(old))
                    {
                        if (_associativeValues != null) _associativeValues.TryRemove(old, out _);
                        else if (_linearKeys != null)
                        {
                            for (int i = 0; i < _linearCount; i++)
                            {
                                if (_linearKeys[i].Equals(old))
                                {
                                    _linearKeys[i] = _linearKeys[_linearCount - 1];
                                    _linearValues![i] = _linearValues![_linearCount - 1];
                                    _linearKeys[_linearCount - 1] = default;
                                    _linearValues![_linearCount - 1] = default;
                                    _linearCount--;
                                    break;
                                }
                            }
                        }
                    }
                    AddCount(value);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(DreamValue value)
        {
            _lock.EnterReadLock();
            try
            {
                return ContainsInternal(value);
            }
            finally
            {
                _lock.ExitReadLock();
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
            // Associative lookup is now lock-free if dictionary exists
            var assoc = _associativeValues;
            if (assoc != null)
            {
                return assoc.TryGetValue(key, out var value) ? value : DreamValue.Null;
            }

            // Linear search for tiered storage
            var keys = _linearKeys;
            if (keys != null)
            {
                for (int i = 0; i < _linearCount; i++)
                {
                    if (keys[i].Equals(key)) return _linearValues![i];
                }
            }

            return DreamValue.Null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DreamValue GetValue(int index)
        {
            _lock.EnterReadLock();
            try
            {
                if (index >= 0 && index < _values.Count) return _values[index];
                return DreamValue.Null;
            }
            finally
            {
                _lock.ExitReadLock();
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
