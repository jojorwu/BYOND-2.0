using Shared;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Shared
{
    public class DreamObject : IDisposable
    {
        protected readonly ReaderWriterLockSlim _lock = new();
        public ObjectType? ObjectType { get; set; }
        protected DreamValue[] _variableValues;
        public long Version { get; protected set; }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual DreamValue GetVariable(string name)
        {
            if (ObjectType == null) return DreamValue.Null;
            int index = ObjectType.GetVariableIndex(name);
            if (index == -1) return DreamValue.Null;

            _lock.EnterReadLock();
            try
            {
                var values = _variableValues;
                if (index < values.Length) return values[index];
                return DreamValue.Null;
            }
            finally
            {
                _lock.ExitReadLock();
            }
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
            _lock.EnterReadLock();
            try
            {
                var values = _variableValues;
                if (index >= 0 && index < values.Length)
                    return values[index];
                return DreamValue.Null;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DreamValue GetVariableDirect(int index)
        {
            _lock.EnterReadLock();
            try
            {
                var values = _variableValues;
                if (index >= 0 && index < values.Length)
                    return values[index];
                return DreamValue.Null;
            }
            finally
            {
                _lock.ExitReadLock();
            }
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

            _lock.EnterWriteLock();
            try
            {
                if (index >= _variableValues.Length)
                {
                    System.Array.Resize(ref _variableValues, index + 1);
                }

                if (!_variableValues[index].Equals(value))
                {
                    _variableValues[index] = value;
                    Version++;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public override string ToString()
        {
            return ObjectType?.Name ?? "object";
        }

        public virtual void Dispose()
        {
            _lock.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
