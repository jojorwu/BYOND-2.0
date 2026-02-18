using Shared;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Shared
{
    /// <summary>
    /// Represents a dynamic object in the Dream VM.
    /// Provides thread-safe, lock-free reads for variables using a Copy-on-Write (COW) pattern.
    /// </summary>
    public class DreamObject : IDisposable
    {
        protected readonly object _lock = new();

        /// <summary>
        /// Gets the type definition of this object.
        /// </summary>
        public ObjectType? ObjectType { get; protected set; }

        protected volatile DreamValue[] _variableValues = System.Array.Empty<DreamValue>();
        private long _version;

        /// <summary>
        /// Gets or sets the version of the object's state. Incremented on every variable change.
        /// </summary>
        public long Version { get => Interlocked.Read(ref _version); set => Interlocked.Exchange(ref _version, value); }

        /// <summary>
        /// Atomically increments the state version.
        /// </summary>
        protected void IncrementVersion() => Interlocked.Increment(ref _version);

        public DreamObject(ObjectType? objectType)
        {
            Initialize(objectType);
        }

        protected void Initialize(ObjectType? objectType)
        {
            ObjectType = objectType;
            if (objectType != null)
            {
                int count = objectType.VariableNames.Count;
                if (_variableValues == null || _variableValues.Length < count)
                {
                    _variableValues = count > 0 ? new DreamValue[count] : System.Array.Empty<DreamValue>();
                }
                else
                {
                    Array.Clear(_variableValues, 0, _variableValues.Length);
                }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual DreamValue GetVariable(string name)
        {
            if (ObjectType == null) return DreamValue.Null;
            int index = ObjectType.GetVariableIndex(name);
            if (index == -1) return DreamValue.Null;

            // Lock-free read via volatile array
            var values = _variableValues;
            if (index < values.Length) return values[index];
            return DreamValue.Null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void SetVariable(string name, DreamValue value)
        {
            if (ObjectType == null) return;
            int index = ObjectType.GetVariableIndex(name);
            if (index != -1)
            {
                SetVariableDirect(index, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DreamValue GetVariable(int index)
        {
            // Lock-free read via volatile array
            var values = _variableValues;
            if (index >= 0 && index < values.Length)
                return values[index];
            return DreamValue.Null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DreamValue GetVariableDirect(int index)
        {
            // Lock-free read via volatile array
            var values = _variableValues;
            if (index >= 0 && index < values.Length)
                return values[index];
            return DreamValue.Null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVariable(int index, DreamValue value)
        {
            SetVariableDirect(index, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void SetVariableDirect(int index, DreamValue value)
        {
            if (index < 0) return;

            lock (_lock)
            {
                var currentValues = _variableValues;
                if (index >= currentValues.Length)
                {
                    var newValues = new DreamValue[index + 1];
                    System.Array.Copy(currentValues, newValues, currentValues.Length);
                    newValues[index] = value;
                    _variableValues = newValues; // Volatile swap
                    IncrementVersion();
                }
                else if (!currentValues[index].Equals(value))
                {
                    // Copy-on-Write to avoid tearing and ensure consistency for concurrent readers
                    var newValues = (DreamValue[])currentValues.Clone();
                    newValues[index] = value;
                    _variableValues = newValues; // Volatile swap
                    IncrementVersion();
                }
            }
        }

        public override string ToString()
        {
            return ObjectType?.Name ?? "object";
        }

        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
