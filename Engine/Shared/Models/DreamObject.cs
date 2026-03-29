using Shared;
using Shared.Interfaces;
using Shared.Services;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Shared;
    public class DreamObject : IDisposable, IBindable
    {
        protected readonly System.Threading.ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);
        protected IUiBindingService? _bindingService;
        public virtual ObjectType? ObjectType { get; set; }

        protected readonly IVariableStore _variableStore;
        protected readonly IVariableStore _committedStore;

        /// <summary>
        /// Gets the committed variable store, containing a thread-safe snapshot for read-only access.
        /// </summary>
        public IVariableStore CommittedStore => _committedStore;

        private long _version;
        public long Version { get => Interlocked.Read(ref _version); set => Interlocked.Exchange(ref _version, value); }
        protected virtual void IncrementVersion() => Interlocked.Increment(ref _version);

        public DreamObject(ObjectType? objectType)
        {
            _variableStore = new TieredVariableStore();
            _committedStore = new TieredVariableStore();

            if (objectType != null && objectType.DefaultValuesArray == null) objectType.FinalizeVariables();
            Initialize(objectType);
        }

        protected void Initialize(ObjectType? objectType)
        {
            ObjectType = objectType;
            if (objectType != null)
            {
                var defaults = objectType.DefaultValuesArray;
                if (defaults != null)
                {
                    if (_variableStore is TieredVariableStore tiered) tiered.SetDefaults(defaults);
                    else _variableStore.CopyFrom(defaults);

                    if (_committedStore is TieredVariableStore tieredCommitted) tieredCommitted.SetDefaults(defaults);
                    else _committedStore.CopyFrom(defaults);
                }
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
                return _variableStore.Get(index);
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
                return _variableStore.Get(index);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual DreamValue GetVariableDirect(int index)
        {
            _lock.EnterReadLock();
            try
            {
                return _variableStore.Get(index);
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
        public virtual void SetVariableDirect(int index, DreamValue value, bool suppressVersion = false)
        {
            if ((uint)index >= 1000000) return; // Basic sanity check

            _lock.EnterWriteLock();
            try
            {
                var current = _variableStore.Get(index);
                if (!current.Equals(value))
                {
                    _variableStore.Set(index, value);
                    if (!suppressVersion)
                    {
                        IncrementVersion();
                    }

                    var binding = _bindingService;
                    if (binding != null)
                    {
                        binding.NotifyPropertyChanged(this, index, value);
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void SetBindingService(IUiBindingService bindingService)
        {
            _bindingService = bindingService;
        }

        public override string ToString()
        {
            return ObjectType?.Name ?? "object";
        }

        public virtual void Dispose()
        {
            _variableStore.Dispose();
            _committedStore.Dispose();
            _lock.Dispose();
            GC.SuppressFinalize(this);
        }
    }
